using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data;
using ReqSaaS_1.Data.Entities;
using ReqSaaS_1.Models;
using ReqSaaS_1.Services.BCN;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading;

namespace ReqSaaS_1.Controllers
{
    [Authorize(Policy = "Nivel2Plus")]
    [Route("import/bcn")]
    public class ImportController : Controller
    {
        private readonly IBCNClient _bcn;
        private readonly AppDbContext _db;

        public ImportController(IBCNClient bcn, AppDbContext db)
        {
            _bcn = bcn;
            _db = db;
        }

        // =========================
        // GET import/bcn/search?q=...
        // (Proyecto: búsqueda por ID de norma)
        // =========================
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
        {
            var query = (q ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Falta el parámetro q.");

            if (!int.TryParse(query, out var idNorma))
                return Json(Array.Empty<BCNSearchResultDto>());

            var xml = await _bcn.GetNormaXmlAsync(idNorma, ct);
            if (xml is null) return Json(Array.Empty<BCNSearchResultDto>());

            var dto = RequisitoImportParser.Parse(
                idNorma,
                xml,
                $"https://www.leychile.cl/Consulta/obtxml?opt=7&idNorma={idNorma}"
            );

            // 👉 Componer el mismo título que usas al guardar
            var numero = ExtraerNumeroNorma(xml);
            var tituloFinal = ComposeTitulo(dto.Tipo, numero, dto.Titulo);

            return Json(new[]
            {
        new BCNSearchResultDto
        {
            NormaIDBCN = idNorma,
            Titulo     = tituloFinal, // <-- devolvemos el compuesto
            Entidad    = dto.Entidad,
            Tipo       = dto.Tipo
        }
    });
        }


        // =========================
        // POST import/bcn/requisitos/{idNorma}
        // Crea/actualiza Requisito + DetalleEvaluacion (uno por artículo)
        // =========================
        [HttpPost("/import/bcn/requisitos/{idNorma:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRequisito(int idNorma, CancellationToken ct)
        {
            // 0) Asegura organización (claim "rut")
            var idOrganismo = User.FindFirst("rut")?.Value;
            if (string.IsNullOrWhiteSpace(idOrganismo))
                return Unauthorized("Falta el claim 'rut' del usuario.");

            // 1) Trae XML desde BCN
            var xml = await _bcn.GetNormaXmlAsync(idNorma, ct);
            if (xml is null) return BadRequest("No se pudo obtener XML de BCN.");

            // 2) Parse básico (título, entidad, tipo, descripción)
            var url = $"https://www.leychile.cl/Consulta/obtxml?opt=7&idNorma={idNorma}";
            var dto = RequisitoImportParser.Parse(idNorma, xml, url);

            // 3) Determina ID_tipo desde la tabla maestra (nombre exacto, normalizado)
            static string NormalizeKey(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                s = s.Trim().ToLowerInvariant()
                     .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
                     .Replace("ü", "u").Replace("ñ", "n");
                return s;
            }

            int? idTipo = null;
            {
                var dtoKey = NormalizeKey(dto.Tipo ?? "");
                // Trae todos una sola vez (catálogo pequeño y estable)
                var tipos = await _db.Tipos.AsNoTracking().ToListAsync(ct);

                var match = tipos.FirstOrDefault(t => NormalizeKey(t.Nombre) == dtoKey);
                if (match != null)
                    idTipo = match.IdTipo;
                else
                {
                    var otro = tipos.FirstOrDefault(t => NormalizeKey(t.Nombre) == "otro");
                    idTipo = otro?.IdTipo; // fallback a "OTRO" si existe, si no queda null
                }
            }
            var numero = ExtraerNumeroNorma(xml);
            var tituloFinal = ComposeTitulo(dto.Tipo, numero, dto.Titulo);

            // 4) Upsert del REQUISITO por (norma + organismo)
            var requisito = await _db.Requisitos
                .FirstOrDefaultAsync(r => r.NormaIDBCN == idNorma && r.IdOrganismo == idOrganismo, ct);

            var isCreate = false;
            if (requisito == null)
            {
                requisito = new Requisito
                {
                    Titulo = tituloFinal,
                    Descripcion = dto.Descripcion,
                    Entidad = dto.Entidad,
                    PorcentajeCumplimiento = 0,
                    IdOrganismo = idOrganismo,   // vínculo al usuario logueado
                    NormaIDBCN = idNorma,
                    IdTipo = idTipo
                };
                _db.Requisitos.Add(requisito);
                isCreate = true;
            }
            else
            {
                requisito.Titulo = tituloFinal;
                requisito.Descripcion = dto.Descripcion;
                requisito.Entidad = dto.Entidad;
                requisito.IdTipo = idTipo;
            }

            await _db.SaveChangesAsync(ct); // asegura ID_requisito

            // 5) Extrae artículos del XML y pobla DetalleEvaluacion sin duplicar
            var articulos = ExtraerArticulos(xml);

            var existentes = await _db.DetalleEvaluaciones
                .Where(d => d.IdRequisito == requisito.IdReq)
                .Select(d => d.Detalle)
                .ToListAsync(ct);

            var setExistentes = existentes
                .Select(Normalize)
                .ToHashSet(StringComparer.Ordinal);

            var nuevos = new List<DetalleEvaluacion>();
            foreach (var art in articulos)
            {
                var norm = Normalize(art);
                if (string.IsNullOrWhiteSpace(norm)) continue;
                if (setExistentes.Contains(norm)) continue;

                nuevos.Add(new DetalleEvaluacion
                {
                    IdRequisito = requisito.IdReq,
                    Detalle = art,
                    Cumplimiento = false,   // el usuario lo definirá después
                    Justificacion = null,   // lo llenará después
                    ArchivoUrl = null
                });
            }

            if (nuevos.Count > 0)
            {
                _db.DetalleEvaluaciones.AddRange(nuevos);
                await _db.SaveChangesAsync(ct);
            }

            // 6) Recalcula % cumplimiento
            var tot = await _db.DetalleEvaluaciones.CountAsync(d => d.IdRequisito == requisito.IdReq, ct);
            var ok = await _db.DetalleEvaluaciones.CountAsync(d => d.IdRequisito == requisito.IdReq && d.Cumplimiento, ct);

            var pctInt = tot == 0 ? 0 : (int)Math.Round(100.0 * ok / Math.Max(1, tot), 0);
            requisito.PorcentajeCumplimiento = pctInt;
            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                status = isCreate ? "created" : "updated",
                requisitoId = requisito.IdReq,
                titulo = requisito.Titulo,
                totalArticulos = articulos.Count,
                agregados = nuevos.Count,
                existentes = articulos.Count - nuevos.Count,
                porcentaje = requisito.PorcentajeCumplimiento
            });
        }

        // =========================
        // Helper: extrae artículos desde el XML de BCN
        // =========================
        private static List<string> ExtraerArticulos(string xml)
        {
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://www.leychile.cl/esquemas";

            // <EstructuraFuncional tipoParte="Artículo"><Texto>...</Texto>
            var textos = doc
                .Descendants(ns + "EstructuraFuncional")
                .Where(n =>
                {
                    var tp = (string?)n.Attribute("tipoParte") ?? string.Empty;
                    // “Artículo” (con o sin tilde)
                    return tp.Contains("rtículo", StringComparison.OrdinalIgnoreCase)
                        || tp.Contains("Articulo", StringComparison.OrdinalIgnoreCase);
                })
                .Select(n => (string?)n.Element(ns + "Texto") ?? string.Empty)
                .Select(t => WebUtility.HtmlDecode(t).Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            return textos;
        }
        // Extrae el <Numero> (o variantes) desde el XML de BCN
        private static string? ExtraerNumeroNorma(string xml)
        {
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://www.leychile.cl/esquemas";

            // Intentos con nombres más comunes
            foreach (var tag in new[] { "Numero", "NumeroNorma", "NumeroOficial" })
            {
                var val = doc.Descendants(ns + tag)
                             .Select(n => (string?)n.Value)
                             .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (!string.IsNullOrWhiteSpace(val))
                    return val!.Trim();
            }

            // Fallback por nombre local (por si cambia el namespace)
            var any = doc.Descendants()
                         .FirstOrDefault(e =>
                             e.Name.LocalName.Equals("Numero", StringComparison.OrdinalIgnoreCase) ||
                             e.Name.LocalName.Contains("Numero", StringComparison.OrdinalIgnoreCase));
            var v2 = any?.Value?.Trim();
            return string.IsNullOrWhiteSpace(v2) ? null : v2;
        }

        // Arma: "{Tipo} N° {Numero} — {TítuloActual}"
        private static string ComposeTitulo(string? tipo, string? numero, string? tituloActual)
        {
            var t = (tipo ?? "").Trim();
            var n = (numero ?? "").Trim();
            var baseTitulo = (tituloActual ?? "").Trim();

            string prefix = string.Empty;
            if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(n)) prefix = $"{t} N° {n}";
            else if (!string.IsNullOrEmpty(t)) prefix = t;
            else if (!string.IsNullOrEmpty(n)) prefix = $"N° {n}";

            if (string.IsNullOrEmpty(prefix)) return baseTitulo;
            if (string.IsNullOrEmpty(baseTitulo)) return prefix;
            return $"{prefix} - {baseTitulo}";
        }


        // Normaliza espacios para comparar textos iguales con distinto espaciado
        private static string Normalize(string s)
            => Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();
    }
}
