using ClaimSystem.Data;
using ClaimSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor Pages (Identity UI uses Razor Pages)
builder.Services.AddControllersWithViews(options =>
{
    // Require authenticated users by default
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddRazorPages();

// Ensure Identity cookie redirects to the login page
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Identity/Account/Login";
    opts.AccessDeniedPath = "/Identity/Account/AccessDenied";
});
builder.Services.AddRazorPages();

// DbContexts
builder.Services.AddDbContext<ClaimDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<AppIdentityDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity with default UI (no scaffolding needed)
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
    .AddDefaultTokenProviders()
    .AddDefaultUI();   // <-- enables /Identity/Account/... pages

var app = builder.Build();

// Apply migrations & seed (as you already have)
using (var scope = app.Services.CreateScope())
{
    var claimsDb = scope.ServiceProvider.GetRequiredService<ClaimDbContext>();
    claimsDb.Database.Migrate();

    var idDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    idDb.Database.Migrate();

    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();  // must be before Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Claims}/{action=Index}/{id?}");

app.MapRazorPages();      // <-- required for the default Identity UI

app.Run();
