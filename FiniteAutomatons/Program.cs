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
// register concrete generator and decorated interface
builder.Services.AddScoped<AutomatonGeneratorService>();
builder.Services.AddScoped<IAutomatonGeneratorService>(sp =>
{
    var inner = sp.GetRequiredService<AutomatonGeneratorService>();
    var audit = sp.GetRequiredService<IAuditService>();
    return new FiniteAutomatons.Services.Observability.AutomatonGeneratorServiceAuditorDecorator(inner, audit);
});

builder.Services.AddScoped<IAutomatonTempDataService, AutomatonTempDataService>();
builder.Services.AddScoped<IHomeAutomatonService, HomeAutomatonService>();
builder.Services.AddScoped<IAutomatonValidationService, AutomatonValidationService>();
// register concrete conversion and decorated interface
builder.Services.AddScoped<AutomatonConversionService>();
builder.Services.AddScoped<IAutomatonConversionService>(sp =>
{
    var inner = sp.GetRequiredService<AutomatonConversionService>();
    var audit = sp.GetRequiredService<IAuditService>();
    return new FiniteAutomatons.Services.Observability.AutomatonConversionServiceAuditorDecorator(inner, audit);
});

// register concrete execution service
builder.Services.AddScoped<AutomatonExecutionService>();
// register decorated interface
builder.Services.AddScoped<IAutomatonExecutionService>(sp =>
{
    var inner = sp.GetRequiredService<AutomatonExecutionService>();
    var audit = sp.GetRequiredService<IAuditService>();
    return new FiniteAutomatons.Services.Observability.AutomatonExecutionServiceAuditorDecorator(inner, audit);
});

builder.Services.AddScoped<IAutomatonBuilderService, AutomatonBuilderService>();
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
    // Use global exception handler in non-development environments
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var auditSvc = context.RequestServices.GetService<IAuditService>();

            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = feature?.Error;
            var path = feature?.Path;

            if (exception != null)
            {
                logger.LogError(exception, "Unhandled exception occurred while processing request for {Path}", path);
                if (auditSvc != null)
                {
                    await auditSvc.AuditAsync("UnhandledException", path ?? "unknown", new Dictionary<string, string?> { ["Exception"] = exception.ToString() });
                }
            }

            // Return ProblemDetails for API clients
            context.Response.StatusCode = 500;
            var accept = context.Request.Headers["Accept"].ToString();
            if (accept != null && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var problem = new ProblemDetails
                {
                    Status = 500,
                    Title = "An unexpected error occurred",
                    Detail = "An internal server error occurred."
                };
                context.Response.ContentType = "application/problem+json";
                await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, problem);
            }
            else
            {
                // For browser requests, redirect to friendly error page
                context.Response.Redirect("/Home/Error");
            }
        });
    });

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

await app.RunAsync();

public partial class Program { }