using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data;
using System;

var builder = WebApplication.CreateBuilder(args);

// === MVC + filtro global para evitar caché en vistas (clave para que "Atrás" no muestre páginas privadas) ===
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new ResponseCacheAttribute
    {
        NoStore = true,
        Location = ResponseCacheLocation.None
    });
});

// === EF Core + PostgreSQL (usa ConnectionStrings:DefaultConnection) ===
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(cs))
    throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection en appsettings.json");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(cs));

// === Cookies de autenticación (Opción A: vida corta y sin sliding) ===
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Home/Index";                 // pantalla de login
        o.LogoutPath = "/Home/Logout";               // debe ser POST en el controlador
        o.AccessDeniedPath = "/Home/Index";          // o una vista de acceso denegado dedicada

        // Vida corta y SIN sliding → exige re-login con frecuencia
        o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        o.SlidingExpiration = false;

        // Configuración de la cookie
        o.Cookie.Name = "ReqSaaS.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax; // POST→GET mismo sitio ok
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Secure bajo HTTPS
    });

// === Autorización por políticas (según tu "nivel") ===
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Nivel2Plus", p =>
        p.RequireAssertion(ctx => ctx.User.HasClaim("nivel", "2") || ctx.User.HasClaim("nivel", "3")));
    options.AddPolicy("Nivel3Only", p => p.RequireClaim("nivel", "3"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Autenticación primero
app.UseAuthentication();
app.UseAuthorization();

// === Refuerzo anti–caché sin romper cuando la respuesta ya empezó ===
app.Use(async (ctx, next) =>
{
    // Registrar callback para agregar headers justo antes de enviar la respuesta
    ctx.Response.OnStarting(() =>
    {
        if (ctx.User?.Identity?.IsAuthenticated == true)
        {
            ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            ctx.Response.Headers["Pragma"] = "no-cache";
            ctx.Response.Headers["Expires"] = "0";
        }
        return Task.CompletedTask;
    });

    await next();
});


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
