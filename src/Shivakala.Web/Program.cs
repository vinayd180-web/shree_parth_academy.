using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shivakala.Application.Interfaces;
using Shivakala.Infrastructure.Data;
using Shivakala.Infrastructure.Repositories;
using Shivakala.Infrastructure.Services;
using Shivakala.Web.Resources;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// --- Database Connection Logic [Fix] ---
var host = Environment.GetEnvironmentVariable("DB_HOST");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var port = Environment.GetEnvironmentVariable("DB_PORT")?? "5432";
var user = Environment.GetEnvironmentVariable("DB_USER");
var pass = Environment.GetEnvironmentVariable("DB_PASSWORD");

string connectionString;
if (!string.IsNullOrEmpty(host))
{
    connectionString = $"Host={host};Database={dbName};Port={port};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
    Console.WriteLine($"[Fix] DB from DB_HOST vars Host={host} DB={dbName}");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")?? "";
    Console.WriteLine("[Fix] DB from DefaultConnection");
}

builder.Services.AddDbContext<ShivakalaDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Your Original Services ---
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

// --- TABLE CREATE FIX - Isse Courses does not exist khatam hoga ---
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ShivakalaDbContext>();
        db.Database.EnsureCreated();
        Console.WriteLine(">>> TABLES CREATED SUCCESSFULLY <<<");
    }
}
catch (Exception ex)
{
    Console.WriteLine($">>> FAILED: {ex.Message} <<<");
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
