using Microsoft.AspNetCore.Mvc;
using HU1_Date.Models;
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

    // Acci�n GET para obtener feriados desde una API externa
    [HttpGet]
    public async Task<IActionResult> GetFeriados()
    {
        var url = "https://api.boostr.cl/holidays.json";

        try
        {
            using var client = new HttpClient();
            var response = await client.GetStringAsync(url);

            var feriados = JsonSerializer.Deserialize<List<Feriado>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return Json(feriados);
        }
        catch
        {
            return StatusCode(500, "No se pudo cargar la informaci�n de los feriados.");
        }
    }

    // Acci�n POST para procesar el login
    [HttpPost]
    public IActionResult Login(string email, string password)
    {
        // Autenticaci�n b�sica 
        if (email == "admin@example.com" && password == "1234")
        {
            return RedirectToAction("ReqView");
        }

        // Si no es v�lido, vuelve a Index con un error
        ViewData["Error"] = "Correo o contrase�a incorrectos";
        return View("Index");
    }

    // abrir detalles
    [HttpGet]
    public IActionResult ReqView_Details()
    {
       
        return View("reqView_Details");
    }

    // Vista a mostrar despu�s del login exitoso
    public IActionResult ReqView()
    {
        return View(); // Aseg�rate de tener Views/Home/ReqView.cshtml
    }
}
