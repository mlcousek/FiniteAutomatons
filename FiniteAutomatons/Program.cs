using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;

// Set console encoding to UTF-8 to properly display Unicode characters like ?
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, "dev.json")))
{
    builder.Configuration.AddJsonFile("dev.json", optional: true, reloadOnChange: true);
}

var dbSettings = new DatabaseSettings();
builder.Configuration.GetSection("DatabaseSettings").Bind(dbSettings);
var connectionString = dbSettings.GetConnectionString();

//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

//var connectionString = "Server=finite_automatons_db,1433;Database=finite_automatons_db;User Id=sa;Password=myStong_Password123#;Trust Server Certificate=True";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Configure logging with better Unicode support
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddScoped<IExecuteService, ExecuteService>();
builder.Services.AddScoped<IAutomatonGeneratorService, AutomatonGeneratorService>();
builder.Services.AddScoped<IAutomatonTempDataService, AutomatonTempDataService>();
builder.Services.AddScoped<IHomeAutomatonService, HomeAutomatonService>();

// Register automaton types
builder.Services.AddTransient<DFA>();
builder.Services.AddTransient<NFA>();
builder.Services.AddTransient<EpsilonNFA>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program { }