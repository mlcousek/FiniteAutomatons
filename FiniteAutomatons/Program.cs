using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Data;
using FiniteAutomatons.Filters;
using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
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

// Ensure folder for traces exists
var tracesPath = Path.Combine(builder.Environment.ContentRootPath, "observability", "traces.log");
var logsPath = Path.Combine(builder.Environment.ContentRootPath, "observability", "logs.log");
var auditsPath = Path.Combine(builder.Environment.ContentRootPath, "observability", "audits.log");
Directory.CreateDirectory(Path.GetDirectoryName(tracesPath) ?? builder.Environment.ContentRootPath);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews(o =>
{
    if (builder.Environment.IsProduction())
    {
        o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    }
    o.Filters.Add<AutomatonModelFilter>();
});

// Configure logging with better Unicode support
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add OpenTelemetry tracing (console exporter for local/dev)
builder.Services.AddOpenTelemetry().WithTracing(tracerProvider =>
{
    tracerProvider
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Environment.ApplicationName ?? "FiniteAutomatons"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter();
});

if (builder.Environment.IsDevelopment())
{
    // In development/tests use in-memory activity collector
    var collector = new InMemoryActivityCollector();
    builder.Services.AddSingleton(collector);
    builder.Services.AddSingleton<InMemoryActivityCollector>(collector);

    // In-memory audit
    var inMemAudit = new InMemoryAuditService();
    builder.Services.AddSingleton<IAuditService>(inMemAudit);
    builder.Services.AddSingleton<InMemoryAuditService>(inMemAudit);
}
else
{
    // Register activity file writer to capture activities to a local file
    builder.Services.AddSingleton(new ActivityFileWriter(tracesPath));

    // Register audit service (moved to Services project)
    builder.Services.AddSingleton<IAuditService>(new FileAuditService(auditsPath));

    // Simple file logger
    builder.Logging.AddProvider(new FileLoggerProvider(logsPath));
}

// Add OpenTelemetry logging to file (kept minimal)
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Environment.ApplicationName ?? "FiniteAutomatons"));
});

builder.Services.AddScoped<IExecuteService, ExecuteService>();
builder.Services.AddScoped<IAutomatonGeneratorService, AutomatonGeneratorService>();
builder.Services.AddScoped<IAutomatonTempDataService, AutomatonTempDataService>();
builder.Services.AddScoped<IHomeAutomatonService, HomeAutomatonService>();
builder.Services.AddScoped<IAutomatonValidationService, AutomatonValidationService>();
builder.Services.AddScoped<IAutomatonConversionService, AutomatonConversionService>();
builder.Services.AddScoped<IAutomatonBuilderService, AutomatonBuilderService>();
builder.Services.AddScoped<IAutomatonExecutionService, AutomatonExecutionService>();
builder.Services.AddScoped<IAutomatonEditingService, AutomatonEditingService>();

// Register automaton types
builder.Services.AddTransient<DFA>();
builder.Services.AddTransient<NFA>();
builder.Services.AddTransient<EpsilonNFA>();

var app = builder.Build();

// Audit app start
var audit = app.Services.GetRequiredService<IAuditService>();
await audit.AuditAsync("ApplicationStart", "Application started", new Dictionary<string, string?> { ["Environment"] = app.Environment.EnvironmentName });

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

// Development-only test endpoint for correlation tests
if (app.Environment.IsDevelopment())
{
    app.MapGet("/_tests/audit-correlation", async (IAuditService audit) =>
    {
        if (audit != null)
        {
            await audit.AuditAsync("TestEndpoint", "Correlation test called");
        }
        return Results.Ok();
    }).WithDisplayName("TestAuditEndpoint");
}

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