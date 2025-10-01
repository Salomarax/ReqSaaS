using Microsoft.AspNetCore.Mvc;
using ReqSaaS_1.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class HomeController : Controller
{
    // Vista principal (Index)
    public IActionResult Index()
    {
        return View();
    }

    // Acción GET para obtener feriados desde una API externa
    [HttpGet]
    public async Task<IActionResult> GetFeriados()
    {
        var url = "https://api.boostr.cl/holidays.json";

        try
        {
            using var client = new HttpClient();
            var json = await client.GetStringAsync(url);

            // 1) Intento directo: array plano [{fecha, nombre, irrenunciable}, ...]
            try
            {
                var direct = JsonSerializer.Deserialize<List<Feriado>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (direct != null && direct.Count > 0)
                    return Json(Normalizar(direct));
            }
            catch { /* Ignorar y probar otras formas */ }

            // 2) Intento genérico: detectar "data", "items", "feriados", etc.
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            IEnumerable<Feriado> extraidos = TryExtract(root);
            var lista = new List<Feriado>(extraidos);
            return Json(Normalizar(lista));
        }
        catch (Exception ex)
        {
            // Si falla la API externa, devolvemos 500 con JSON (para que el front no reviente)
            return StatusCode(500, new { message = "No se pudo cargar la información de los feriados.", detail = ex.Message });
        }
    }

    // --- Helpers ---

    // Acepta varios nombres de campos y normaliza a tu modelo
    private static IEnumerable<Feriado> TryExtract(JsonElement root)
    {
        var salida = new List<Feriado>();

        // caso objeto con arreglo dentro (data/items/feriados/holidays)
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "feriados", "holidays", "data", "items", "result", "results" })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    salida.AddRange(FromArray(arr));
                    return salida;
                }
            }
        }

        // caso arreglo en la raíz
        if (root.ValueKind == JsonValueKind.Array)
        {
            salida.AddRange(FromArray(root));
            return salida;
        }

        // si nada calza, devolver vacío
        return salida;
    }

    private static IEnumerable<Feriado> FromArray(JsonElement arr)
    {
        foreach (var el in arr.EnumerateArray())
        {
            // nombres posibles de campos (fecha/date/day), (nombre/title/name), (irrenunciable/mandatory/etc.)
            string? fecha = GetString(el, "fecha", "date", "day", "fecha_iso");
            string? nombre = GetString(el, "nombre", "title", "name", "descripcion", "description");
            bool irrenunciable = GetBool(el, "irrenunciable", "mandatory", "isHoliday", "obligatorio");

            // Si trae un objeto "date" con "iso"
            if (fecha == null && el.TryGetProperty("date", out var dateObj) && dateObj.ValueKind == JsonValueKind.Object)
            {
                fecha = GetString(dateObj, "iso", "fecha");
            }

            // Normaliza formato de fecha a YYYY-MM-DD si viene con hora
            if (!string.IsNullOrWhiteSpace(fecha))
            {
                if (DateTime.TryParse(fecha, out var dt))
                    fecha = dt.ToString("yyyy-MM-dd");
            }

            if (!string.IsNullOrWhiteSpace(fecha) && !string.IsNullOrWhiteSpace(nombre))
            {
                yield return new Feriado
                {
                    Fecha = fecha,
                    Nombre = nombre,
                    Irrenunciable = irrenunciable
                };
            }
        }
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }

    private static bool GetBool(JsonElement el, params string[] names)
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

    private static List<Feriado> Normalizar(List<Feriado> items)
    {
        // Evita nulos y duplicados simples (mismo día + nombre)
        var list = new List<Feriado>();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in items)
        {
            if (string.IsNullOrWhiteSpace(f.Fecha) || string.IsNullOrWhiteSpace(f.Nombre)) continue;
            var key = $"{f.Fecha}|{f.Nombre}";
            if (set.Add(key)) list.Add(f);
        }
        return list;
    }



// Acción POST para procesar el login
[HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string email, string password)
    {
        // Autenticación básica 
        if (email == "admin@example.com" && password == "1234")
        {
            return RedirectToAction("ReqView");
        }

        // Si no es válido, vuelve a Index con un error
        ViewData["Error"] = "Correo o contraseña incorrectos";
        return View("Index");
    }

    // abrir detalles
    [HttpGet]
    public IActionResult ReqView_Details()
    {
       
        return View();
    }

    // Vista a mostrar después del login exitoso
    public IActionResult ReqView()
    {
        return View(); // Asegúrate de tener Views/Home/ReqView.cshtml
    }

    // GET: Agregar requisito
    [HttpGet]
    public IActionResult AddReq()
    {
        return View(); // Views/Home/AddRequirement.cshtml
    }

    // POST: crear manual
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateRequirement(ReqInputVM vm)
    {
        if (!ModelState.IsValid) return View("AddRequirement");
        // TODO: guardar en DB
        // vm.Titulo, vm.Descripcion, vm.Entidad, vm.Tipo, vm.Item
        return RedirectToAction("ReqView");
    }

    // GET: buscar en BCN (cuando conectemos la API real)
    [HttpGet]
    public async Task<IActionResult> SearchBCN(string q)
    {
        // TODO: llamar API BCN, mapear y devolver JSON [{titulo, entidad, tipo, codigo, item, descripcion}]
        return Json(new object[] { });
    }

    // POST: importar desde BCN (con los hidden del formulario)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportFromBCN(ReqInputVM vm)
    {
        // TODO: guardar en DB directamente lo traído de BCN
        return RedirectToAction("ReqView");
    }

    // POST: subir PDF
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(IFormFile Archivo)
    {
        if (Archivo == null || Archivo.Length == 0)
            return BadRequest("Archivo vacío.");

        // TODO: guardar temporal y procesar (OCR/LLM), poblar campos requeridos y guardar en DB
        return RedirectToAction("ReqView");
    }
}
