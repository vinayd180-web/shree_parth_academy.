using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shivakala.Core.Interfaces;
using Shivakala.Infrastructure.Data;
using Shivakala.Infrastructure.Repositories;
using Shivakala.Infrastructure.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// --- PORT CONFIGURATION ---
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});
Console.WriteLine($"[Port Config] Server listening on port: {port}");

// --- POSTGRESQL DATABASE CONFIGURATION ---
string connectionString = "";

// 1. Check for DATABASE_URL (Railway provides this)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(databaseUrl))
{
    connectionString = databaseUrl;
    Console.WriteLine($"[DB Config] ✅ Using PostgreSQL DATABASE_URL from Railway");
}
else
{
    // 2. Check for individual DB_* variables (fallback)
    var host = Environment.GetEnvironmentVariable("DB_HOST");
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var user = Environment.GetEnvironmentVariable("DB_USER");
    var pass = Environment.GetEnvironmentVariable("DB_PASSWORD");

    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(dbName) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
    {
        connectionString = $"Host={host};Database={dbName};Port={dbPort};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
        Console.WriteLine($"[DB Config] Using PostgreSQL from environment variables");
    }
    else
    {
        // 3. Fallback to appsettings
        connectionString = builder.Configuration.GetConnectionString("PostgreSql") ?? "";
        if (!string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine($"[DB Config] Using PostgreSQL from appsettings");
        }
        else
        {
            Console.WriteLine("[CRITICAL ERROR] No PostgreSQL connection string found!");
            Environment.Exit(1);
        }
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("[CRITICAL ERROR] No database connection string available.");
    Environment.Exit(1);
}

// --- Register DbContext with PostgreSQL ---
builder.Services.AddDbContext<ShivakalaDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Register Services (from Infrastructure) ---
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IHomePageService, HomePageService>();

// --- MVC + Localization ---
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews().AddViewLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options => {
    var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("hi"), new CultureInfo("mr") };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// --- DATABASE MIGRATION ---
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ShivakalaDbContext>();
        db.Database.Migrate();
        Console.WriteLine(">>> POSTGRESQL DATABASE MIGRATED SUCCESSFULLY <<<");
    }
}
catch (Exception ex)
{
    Console.WriteLine($">>> ERROR: Failed to migrate PostgreSQL database: {ex.Message} <<<");
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShivakalaDbContext>();
            db.Database.EnsureCreated();
            Console.WriteLine(">>> POSTGRESQL TABLES CREATED WITH EnsureCreated <<<");
        }
    }
    catch (Exception ex2)
    {
        Console.WriteLine($">>> ERROR: Failed to create PostgreSQL tables: {ex2.Message} <<<");
    }
}

var locOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
