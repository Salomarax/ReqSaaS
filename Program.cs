using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data;
using System;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// === EF Core + PostgreSQL (usa ConnectionStrings:DefaultConnection) ===
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(cs))
    throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection en appsettings.json");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(cs));   // <- IMPORTANTE

// Cookies de autenticación
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Home/Index";       // o "/Home/Login" si prefieres
        o.LogoutPath = "/Home/Logout";
        o.AccessDeniedPath = "/Home/Index"; // o una vista de acceso denegado
        o.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        o.SlidingExpiration = true;

        // 👉 estas 3 líneas evitan que el browser descarte la cookie en local
        o.Cookie.Name = "ReqSaaS.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;                 // permite POST→GET mismo sitio
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS=Secure, HTTP=no
    });

// Políticas por nivel
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

app.UseAuthentication();   // <- antes de Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
