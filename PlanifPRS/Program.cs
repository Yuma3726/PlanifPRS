using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddAuthentication(Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme);

// ✅ CONFIGURATION POUR GÉRER LES ACCÈS REFUSÉS
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied"; // ✅ CHEMIN SIMPLE
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddDbContext<PlanifPrsDbContext>(options =>
    options.UseSqlServer("Server=MSLTest20\\test;Database=PlanifPRS;User Id=ssis;Password=ssis;TrustServerCertificate=True;Encrypt=True;"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ MIDDLEWARE POUR REDIRIGER LES 403
app.UseStatusCodePagesWithRedirects("/AccessDenied?code={0}");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();