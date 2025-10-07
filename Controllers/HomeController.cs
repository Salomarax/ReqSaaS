using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReqSaaS_1.Data;
using ReqSaaS_1.Data.Entities;
using ReqSaaS_1.Models;
using ReqSaaS_1.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    // --- HOME (pantalla de login) ---
    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Index() => View(new LoginVM());

    // --- FERIADOS ---
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetFeriados()
    {
        const string url = "https://api.boostr.cl/holidays.json";

        try
        {
            using var http = new HttpClient();
            var json = await http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            static string? S(JsonElement el, params string[] names)
            {
                foreach (var n in names)
                    if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();
                return null;
            }
            static bool B(JsonElement el, params string[] names)
            {
                foreach (var n in names)
                {
                    if (el.TryGetProperty(n, out var v))
                    {
                        if (v.ValueKind == JsonValueKind.True) return true;
                        if (v.ValueKind == JsonValueKind.False) return false;
                        if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b)) return b;
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i != 0;
                    }
                }
                return false;
            }
            static string D(string s)
            {
                if (DateTime.TryParse(s, out var dt)) return dt.ToString("yyyy-MM-dd");
                s = s.Replace('/', '-');
                return s.Length >= 10 ? s[..10] : s;
            }

            IEnumerable<JsonElement> rows = Array.Empty<JsonElement>();
            if (root.ValueKind == JsonValueKind.Array) rows = root.EnumerateArray();
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var k in new[] { "feriados", "holidays", "data", "items", "result", "results" })
                    if (root.TryGetProperty(k, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    { rows = arr.EnumerateArray(); break; }
            }

            var list = new List<object>();
            foreach (var el in rows)
            {
                var fecha = S(el, "fecha", "date", "day", "fecha_iso");
                if (fecha == null && el.TryGetProperty("date", out var dobj) && dobj.ValueKind == JsonValueKind.Object)
                    fecha = S(dobj, "iso", "fecha");

                var nombre = S(el, "nombre", "title", "name", "descripcion", "description");
                var irr = B(el, "irrenunciable", "mandatory", "isHoliday", "obligatorio");

                if (!string.IsNullOrWhiteSpace(fecha) && !string.IsNullOrWhiteSpace(nombre))
                    list.Add(new { Fecha = D(fecha), Nombre = nombre, Irrenunciable = irr });
            }

            return Json(list);
        }
        catch
        {
            return Json(Array.Empty<object>());
        }
    }

    // --- LOGIN (GET/POST) ---
    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("Index", new LoginVM());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(LoginVM model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            model.Password = string.Empty;
            return View("Index", model);
        }

        var normalized = RutUtils.Normalize(model.Rut);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            await Task.Delay(250);
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            model.Password = string.Empty;
            return View("Index", model);
        }

        var candidatos = await _db.Credenciales
            .AsNoTracking()
            .Where(c => c.IdOrganismo == normalized)
            .ToListAsync();

        Credencial? match = null;
        foreach (var c in candidatos)
        {
            if (!string.IsNullOrWhiteSpace(c.ClaveHash) &&
                BCrypt.Net.BCrypt.Verify(model.Password, c.ClaveHash))
            {
                match = c;
                break;
            }
        }

        if (match == null)
        {
            await Task.Delay(250);
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            model.Password = string.Empty;
            return View("Index", model);
        }

        var nivel = (match.IdNivel ?? 1).ToString();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, match.Nombre ?? normalized),
            new Claim("rut", match.IdOrganismo),
            new Claim("nivel", nivel)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true
            });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(reqView));
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpGet("/Home/AddReq")] // ← ruta explícita; evita ambigüedades
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AddReq() => View("AddReq");


    // --- LOGOUT ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return RedirectToAction(nameof(Index));
    }

    // --- Vista resumen ---
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult reqView()
    {
        var nivel = User.FindFirst("nivel")?.Value ?? "1";
        ViewBag.Nivel = nivel;
        ViewBag.CanCrud = nivel == "2" || nivel == "3";
        return View();
    }

    // --- Ping de sesión (debug rápido) ---
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var rut = User.FindFirst("rut")?.Value ?? "(sin rut)";
        var nivel = User.FindFirst("nivel")?.Value ?? "(sin nivel)";
        return Ok(new { ok = true, rut, nivel, now = DateTime.UtcNow });
    }


    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> RequisitosResumen(CancellationToken ct)
    {
        var idOrganismo = User.FindFirst("rut")?.Value ?? "";
        try
        {
            // 1) Build query once to poder ver el SQL exacto
            var qBase = _db.Requisitos
                .Where(r => r.IdOrganismo == idOrganismo)
                .Select(r => new
                {
                    r.IdReq,
                    r.Titulo,
                    r.Entidad,
                    r.NormaIDBCN, // <-- debe mapear a columna "normaID_BCN"
                    r.IdTipo
                });

            // 2) LOG: SQL generado por EF (mira la consola/Output)
            var sql = qBase.ToQueryString();
            Console.WriteLine("\n[RequisitosResumen] SQL generado por EF:\n" + sql + "\n");

            var baseData = await qBase.ToListAsync(ct);

            var qAgg = _db.DetalleEvaluaciones
                .GroupBy(d => d.IdRequisito)
                .Select(g => new { RequisitoId = g.Key, Total = g.Count(), Cumplidos = g.Count(x => x.Cumplimiento) });

            Console.WriteLine("\n[RequisitosResumen] SQL agregados:\n" + qAgg.ToQueryString() + "\n");

            var totales = await qAgg.ToListAsync(ct);
            var map = totales.ToDictionary(x => x.RequisitoId, x => x);

            var salida = baseData.Select(x =>
            {
                map.TryGetValue(x.IdReq, out var agg);
                var total = agg?.Total ?? 0;
                var ok = agg?.Cumplidos ?? 0;
                var porcentaje = total == 0 ? 0 : Math.Round(100.0 * ok / total, 2);

                return new
                {
                    IdReq = x.IdReq,
                    Titulo = x.Titulo ?? "",
                    Entidad = x.Entidad,
                    NormaIDBCN = x.NormaIDBCN,   // si sale null, la vista mostrará "—"
                    IdTipo = x.IdTipo,
                    Total = total,
                    Cumplidos = ok,
                    Porcentaje = porcentaje
                };
            });

            return Ok(salida);
        }
        catch (PostgresException pgex)
        {
            // Devuelve detalle claro al front y log completo en consola
            Console.Error.WriteLine($"[RequisitosResumen][PG] {pgex.SqlState} {pgex.MessageText}\n{pgex}");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Postgres error en el resumen.",
                sqlstate = pgex.SqlState,
                detail = pgex.MessageText
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RequisitosResumen][EX] {ex.GetType().Name}: {ex.Message}\n{ex}");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Fallo al construir el resumen.",
                detail = ex.Message
            });
        }
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> Tipos(CancellationToken ct)
    {
        var tipos = await _db.Tipos
            .OrderBy(t => t.Nombre)
            .Select(t => new { id = t.IdTipo, nombre = t.Nombre })
            .ToListAsync(ct);

        return Ok(tipos);
    }

    [HttpGet]
    public async Task<IActionResult> ReqDetails(int id, bool ro = false, CancellationToken ct = default)
    {
        var idOrganismo = User.FindFirst("rut")?.Value ?? "";
        var nivel = User.FindFirst("nivel")?.Value ?? "1";
        var canCrud = (nivel == "2" || nivel == "3");

        var vm = await _db.Requisitos
            .Where(r => r.IdReq == id && r.IdOrganismo == idOrganismo)
            .Select(r => new RequisitoDetalleVM
            {
                IdReq = r.IdReq,
                Titulo = r.Titulo ?? "",
                Entidad = r.Entidad,
                IdTipo = r.IdTipo,
                TipoNombre = _db.Set<Tipo>().Where(t => t.IdTipo == r.IdTipo).Select(t => t.Nombre).FirstOrDefault(),
                NormaIDBCN = r.NormaIDBCN
            })
            .FirstOrDefaultAsync(ct);

        if (vm == null) return NotFound();

        vm.Items = await _db.DetalleEvaluaciones
            .Where(d => d.IdRequisito == id)
            .OrderBy(d => d.IdItem)
            .ToListAsync(ct);

        // Modo lectura si viene ?ro=true o si el usuario no puede editar
        ViewBag.ReadOnly = ro || !canCrud;

        return View("reqView_Details", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReqDetails(RequisitoDetalleVM model, CancellationToken ct)
    {
        var idOrganismo = User.FindFirst("rut")?.Value ?? "";
        var nivel = User.FindFirst("nivel")?.Value ?? "1";
        var canCrud = (nivel == "2" || nivel == "3");
        var okOwner = await _db.Requisitos
            .AnyAsync(r => r.IdReq == model.IdReq && r.IdOrganismo == idOrganismo, ct);
        if (!canCrud) return Forbid();

        if (!okOwner) return Unauthorized();

        if (model.Items?.Count > 0)
        {
            var ids = model.Items.Select(i => i.IdItem).ToList();

            var dbItems = await _db.DetalleEvaluaciones
                .Where(d => d.IdRequisito == model.IdReq && ids.Contains(d.IdItem))
                .OrderBy(d => d.IdItem)   // ok
                .ToListAsync(ct);

            var map = dbItems.ToDictionary(d => d.IdItem);
            foreach (var vm in model.Items)
            {
                if (map.TryGetValue(vm.IdItem, out var d))
                {
                    d.Cumplimiento = vm.Cumplimiento;
                    d.Justificacion = vm.Justificacion;
                    d.ArchivoUrl = vm.ArchivoUrl;
                }
            }
            await _db.SaveChangesAsync(ct);

            // Recalcula %
            var tot = await _db.DetalleEvaluaciones.CountAsync(d => d.IdRequisito == model.IdReq, ct);
            var cumpl = await _db.DetalleEvaluaciones.CountAsync(d => d.IdRequisito == model.IdReq && d.Cumplimiento, ct);
            var req = await _db.Requisitos.FindAsync(new object[] { model.IdReq }, ct);
            if (req != null)
            {
                var pct = tot == 0 ? 0 : (int)Math.Round(100.0 * cumpl / Math.Max(1, tot), 0);
                try { req.PorcentajeCumplimiento = pct; } catch { }
                await _db.SaveChangesAsync(ct);
            }
        }

        TempData["ok"] = "Cambios guardados.";
        return RedirectToAction(nameof(ReqDetails), new { id = model.IdReq });
    }

}
