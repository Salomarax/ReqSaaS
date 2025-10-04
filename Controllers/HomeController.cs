using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data;
using ReqSaaS_1.Data.Entities;
using ReqSaaS_1.Models;
using ReqSaaS_1.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    // --- HOME (pantalla de login) ---
    // Anónimo para evitar loop con LoginPath = /Home/Index
    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Index()
    {
        // Siempre muestra el formulario de login (no redirige aunque haya cookie)
        return View(new LoginVM());
    }

    // --- FERIADOS (anónimo; robusto ante JSON raro/fallas) ---
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
                    if (el.TryGetProperty(n, out var v))
                    {
                        if (v.ValueKind == JsonValueKind.True) return true;
                        if (v.ValueKind == JsonValueKind.False) return false;
                        if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b)) return b;
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i != 0;
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
                foreach (var k in new[] { "feriados", "holidays", "data", "items", "result", "results" })
                    if (root.TryGetProperty(k, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    { rows = arr.EnumerateArray(); break; }

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

    // --- LOGIN (GET) ---
    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        // Usamos Index.cshtml como vista de login
        return View("Index", new LoginVM());
    }

    // --- LOGIN (POST) ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(LoginVM model, string? returnUrl = null)
    {
        // Validación básica del modelo (requiere campos)
        if (!ModelState.IsValid)
        {
            // Mensaje genérico (no revelar qué falló)
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            model.Password = string.Empty;
            return View("Index", model);
        }

        // Normaliza RUT; si falla, tratamos como credenciales inválidas
        var normalized = RutUtils.Normalize(model.Rut);

        // Recupera candidatos solo si el RUT se pudo normalizar; si no, usa lista vacía
        var candidatos = normalized == null
            ? new List<Credencial>()
            : await _db.Credenciales
                .AsNoTracking()
                .Where(c => c.IdOrganismo == normalized)
                .ToListAsync();

        // Busca coincidencia de contraseña (si no hay candidatos, nunca entra al foreach)
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

        // Si no hubo match → mensaje genérico (no diferenciamos si falló RUT o clave)
        if (match == null)
        {
            // Pequeño retraso uniforme para evitar pistas temporales
            await Task.Delay(250);
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            model.Password = string.Empty;
            return View("Index", model);
        }

        // ===== Autenticación exitosa =====
        var nivel = (match.IdNivel ?? 1).ToString();
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, match.Nombre ?? (normalized ?? model.Rut ?? string.Empty)),
        new Claim("rut", match.IdOrganismo),
        new Claim("nivel", nivel)
    };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true
            });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ReqView));
    }


    // --- LOGOUT (POST con antiforgery; evita CSRF y el botón atrás) ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Refuerzo anti-caché inmediato
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        // Index es la pantalla de login
        return RedirectToAction(nameof(Index));
    }

    // --- Vistas de requisitos (nivel 1 puede ver) ---
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ReqView()   // sin parámetros
    {
        var nivel = User.FindFirst("nivel")?.Value ?? "1";
        ViewBag.Nivel = nivel;
        ViewBag.CanCrud = nivel == "2" || nivel == "3";
        return View();
    }

    // --- CRUD protegido (nivel 2 y 3) ---
    [Authorize(Policy = "Nivel2Plus")]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AddReq()
    {
        return View("AddReq");
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult EditReq(int id)
    {
        // TODO: cargar el requisito y pasarlo a la vista
        return View();
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DeleteReq(int id)
    {
        // TODO: eliminar requisito por id
        return RedirectToAction(nameof(ReqView));
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult CreateRequirement(ReqInputVM vm)
    {
        if (!ModelState.IsValid) return View("AddReq", vm);
        // TODO: guardar en DB
        return RedirectToAction(nameof(ReqView));
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpGet]
    public async Task<IActionResult> SearchBCN(string q)
    {
        // TODO: llamar API BCN, mapear y devolver JSON
        return Json(Array.Empty<object>());
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportFromBCN(ReqInputVM vm)
    {
        // TODO: guardar en DB lo traído de BCN
        return RedirectToAction(nameof(ReqView));
    }

    [Authorize(Policy = "Nivel2Plus")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(IFormFile Archivo)
    {
        if (Archivo == null || Archivo.Length == 0)
            return BadRequest("Archivo vacío.");

        // TODO: procesar archivo
        return RedirectToAction(nameof(ReqView));
    }
}
