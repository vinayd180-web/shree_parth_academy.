using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

// --- DATABASE CONFIGURATION ---
// Check for DATABASE_URL first (Railway)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "PostgreSql";

string connectionString = "";

if (!string.IsNullOrEmpty(databaseUrl))
{
    connectionString = databaseUrl;
    Console.WriteLine($"[DB Config] Using DATABASE_URL from Railway");
}
else
{
    // Fallback to appsettings
    var configProvider = builder.Configuration["Database:Provider"];
    if (configProvider?.ToLower() == "postgresql")
    {
        connectionString = builder.Configuration.GetConnectionString("PostgreSql") ?? "";
        Console.WriteLine($"[DB Config] Using PostgreSQL from appsettings");
    }
    else if (configProvider?.ToLower() == "sqlserver")
    {
        connectionString = builder.Configuration.GetConnectionString("SqlServer") ?? "";
        Console.WriteLine($"[DB Config] Using SQL Server from appsettings");
    }
    else
    {
        connectionString = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=App_Data/shivakala.db";
        Console.WriteLine($"[DB Config] Using SQLite from appsettings");
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("[CRITICAL ERROR] No database connection string available.");
    Environment.Exit(1);
}

builder.Services.AddDbContext<ShivakalaDbContext>(options =>
{
    var provider = builder.Configuration["Database:Provider"]?.ToLower();
    
    if (provider == "postgresql" || !string.IsNullOrEmpty(databaseUrl))
    {
        options.UseNpgsql(connectionString);
    }
    else if (provider == "sqlserver")
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// --- Register Services ---
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IHomePageService, HomePageService>();

// --- MVC Setup ---
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
        Console.WriteLine(">>> DATABASE MIGRATED SUCCESSFULLY <<<");
    }
}
catch (Exception ex)
{
    Console.WriteLine($">>> ERROR: Failed to migrate database: {ex.Message} <<<");
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShivakalaDbContext>();
            db.Database.EnsureCreated();
            Console.WriteLine(">>> TABLES CREATED WITH EnsureCreated <<<");
        }
    }
    catch (Exception ex2)
    {
        Console.WriteLine($">>> ERROR: Failed to create tables: {ex2.Message} <<<");
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
