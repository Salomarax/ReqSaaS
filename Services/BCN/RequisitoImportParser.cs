using ReqSaaS_1.Models;
using System.Globalization;
using System.Xml.Linq;


public static class RequisitoImportParser
{
    private static string? GetFirst(XDocument xdoc, params string[] names)
    {
        if (xdoc?.Root is null || names == null || names.Length == 0) return null;

        // Recorre todos los elementos (incluye Root) y busca el primero cuyo nombre coincida
        foreach (var e in xdoc.Descendants().Prepend(xdoc.Root))
        {
            var name = e.Name.LocalName;
            foreach (var n in names)
            {
                if (string.Equals(name, n, StringComparison.OrdinalIgnoreCase))
                {
                    var val = e.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }
            }
        }
        return null; 
    }

    private static string? GetAttrFirst(XDocument xdoc, params string[] names)
    {
        if (xdoc?.Root is null || names == null || names.Length == 0) return null;

        foreach (var e in xdoc.Descendants().Prepend(xdoc.Root))
        {
            foreach (var n in names)
            {
                var a = e.Attribute(n);
                if (a != null && !string.IsNullOrWhiteSpace(a.Value))
                    return a.Value.Trim();
            }
        }
        return null;
    }

    private static DateTime? TryParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy", "yyyy/MM/dd", "dd/MM/yyyy", "yyyyMMdd" };
        if (DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture,
                                   DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var dt))
            return dt;

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
            return dt;

        return null;
    }

    public static RequisitoImportDTO Parse(int idNorma, string xml, string fuenteUrl)
    {
        var xdoc = XDocument.Parse(xml);

        var titulo = GetFirst(xdoc, "TituloNorma", "tituloNorma", "Titulo");
        var entidad = GetFirst(xdoc, "Organismo", "organismo", "Entidad", "Ministerio");
        var tipo = GetFirst(xdoc, "TipoNorma", "tipoNorma", "Tipo", "TipoDecreto");

        //  FECHAS: primero intenta por ATRIBUTO (ej. en <Norma fechaPromulgacion="2003-06-12" fechaPublicacion="2004-06-16">)
        var promStr = GetAttrFirst(xdoc, "fechaPromulgacion")
                      ?? GetFirst(xdoc, "FechaPromulgacion", "fechaPromulgacion", "Fecha");
        var pubStr = GetAttrFirst(xdoc, "fechaPublicacion")
                      ?? GetFirst(xdoc, "FechaPublicacion", "fechaPublicacion");

        var fechaPromulgacion = TryParseDate(promStr) ?? TryParseDate(pubStr); // usa promulgación,si no, publicación

        var descripcion =
                            GetFirst(xdoc, "Resumen", "Extracto")                              // si existe
                            ?? GetFirst(xdoc, "Encabezado", "encabezado")                      // fallback
                            ?? (GetFirst(xdoc, "Texto", "texto") is string t                  // último recurso
                                ? (t.Length > 300 ? t[..300] + "…" : t) : null);

        return new RequisitoImportDTO
        {
            NormaID_BCN = idNorma,
            Titulo = titulo,
            Entidad = entidad,
            Tipo = tipo,
            FechaPromulgacion = fechaPromulgacion,
            Descripcion = descripcion,
            FuenteUrl = fuenteUrl,
            XmlRaw = xml
        };
    }
}
