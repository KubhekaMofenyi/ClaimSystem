using ClaimSystem.Data;
using ClaimSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ClaimDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<AppIdentityDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(opts =>
    {
        opts.SignIn.RequireConfirmedAccount = false;
        opts.Password.RequiredLength = 6;
        opts.Password.RequireDigit = false;
        opts.Password.RequireNonAlphanumeric = false;
        opts.Password.RequireUppercase = false;
        opts.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();

// Ensure DB exists (good for fresh projects)
using (var scope = app.Services.CreateScope())
{
    var cfg = builder.Configuration;
    var dbPath = Path.GetFullPath(cfg.GetConnectionString("DefaultConnection")!.Replace("Data Source=", ""));
    Console.WriteLine($"[DB] {dbPath}");

    var claimsDb = scope.ServiceProvider.GetRequiredService<ClaimDbContext>();
    claimsDb.Database.Migrate();

    var idDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    idDb.Database.Migrate();

    // Seed roles & demo users (dev-only)
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);


    // Pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();
    app.UseAuthentication();
    app.MapRazorPages();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Claims}/{action=Index}/{id?}");

    app.Run();
}
