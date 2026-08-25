using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shivakala.Infrastructure.Data;
using Shivakala.Infrastructure.Repositories;
using Shivakala.Infrastructure.Services;
using Shivakala.Web.Resources;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// --- PORT CONFIGURATION FIX ---
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});
Console.WriteLine($"[Port Config] Server listening on port: {port}");

// --- Database Connection Logic [Railway Fix] ---
string connectionString = "";
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(databaseUrl))
{
    connectionString = databaseUrl;
    Console.WriteLine($"[DB Config] ✅ Using DATABASE_URL from Railway");
}
else
{
    Console.WriteLine("[DB Config] DATABASE_URL not found, checking individual variables...");
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
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
        if (!string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("[DB Config] Using connection string from appsettings");
        }
        else
        {
            Console.WriteLine("[DB Config] No database connection configured! Using SQLite fallback.");
            connectionString = "Data Source=App_Data/shivakala.db";
        }
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("[CRITICAL ERROR] No database connection string available. Application cannot start.");
    Environment.Exit(1);
}

builder.Services.AddDbContext<ShivakalaDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Register Services ---
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IHomePageService, HomePageService>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) => factory.Create(typeof(SharedResource));
    });

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
