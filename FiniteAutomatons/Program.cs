using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Data;
using FiniteAutomatons.Filters;
using FiniteAutomatons.Middleware;
using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Observability;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;

namespace FiniteAutomatons;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        ConfigureConfiguration(builder);

        var dbSettings = new DatabaseSettings();
        builder.Configuration.GetSection("DatabaseSettings").Bind(dbSettings);
        var connectionString = builder.Configuration.GetConnectionString("finiteautomatonsdb")
                             ?? dbSettings.GetConnectionString();

        var observabilityPaths = EnsureObservabilityPaths(builder.Environment.ContentRootPath);

        ConfigureServices(builder, connectionString, observabilityPaths);

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Audit app start
        using (var scope = app.Services.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await audit.AuditAsync("ApplicationStart", "Application started", new Dictionary<string, string?> { ["Environment"] = app.Environment.EnvironmentName });
        }

        // Apply pending migrations automatically
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                        pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                    await dbContext.Database.MigrateAsync();
                    logger.LogInformation("Migrations applied successfully");
                }
                else
                {
                    logger.LogInformation("Database is up to date, no pending migrations");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying database migrations. The application will continue but database may be out of sync.");
                // Don't crash the app - let it start and show a friendly error page
            }
        }

        ConfigureRequestPipeline(app);

        await app.RunAsync();
    }

    // --------------------------- Helper methods ---------------------------

    private static void ConfigureConfiguration(WebApplicationBuilder builder)
    {
        // Optional local developer configuration
        if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, "dev.json")))
        {
            builder.Configuration.AddJsonFile("dev.json", optional: true, reloadOnChange: true);
        }
    }

    private static (string Traces, string Logs, string Audits) EnsureObservabilityPaths(string contentRoot)
    {
        var tracesPath = Path.Combine(contentRoot, "observability", "traces.log");
        var logsPath = Path.Combine(contentRoot, "observability", "logs.log");
        var auditsPath = Path.Combine(contentRoot, "observability", "audits.log");

        var dir = Path.GetDirectoryName(tracesPath) ?? contentRoot;
        Directory.CreateDirectory(dir);

        return (tracesPath, logsPath, auditsPath);
    }

    private static void ConfigureServices(WebApplicationBuilder builder, string connectionString, (string Traces, string Logs, string Audits) observabilityPaths)
    {
        // Database and Identity
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // MVC and filters
        builder.Services.AddControllersWithViews(o =>
        {
            if (builder.Environment.IsProduction())
            {
                o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            }
            o.Filters.Add<AutomatonModelFilter>();
        });

        // Observability: development uses in-memory collectors, otherwise file-based
        if (builder.Environment.IsDevelopment())
        {
            // In development/tests use in-memory activity collector
            var collector = new InMemoryActivityCollector();
            builder.Services.AddSingleton(collector);
            builder.Services.AddSingleton(collector);

            // In-memory audit
            var inMemAudit = new InMemoryAuditService();
            builder.Services.AddSingleton<IAuditService>(inMemAudit);
            builder.Services.AddSingleton(inMemAudit);

            // Register ActivityListener to forward activities to in-memory collector
            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = a => collector.Add(a),
                ActivityStopped = a => collector.Add(a)
            };
            ActivitySource.AddActivityListener(listener);
        }
        else
        {
            // Register activity file writer to capture activities to a local file
            builder.Services.AddSingleton(new ActivityFileWriter(observabilityPaths.Traces));

            // Register audit service (moved to Services project)
            builder.Services.AddSingleton<IAuditService>(new FileAuditService(observabilityPaths.Audits));

            // Simple file logger
            builder.Logging.AddProvider(new FileLoggerProvider(observabilityPaths.Logs));
        }

        // Application services
        RegisterApplicationServices(builder.Services);

        // register saved automaton service
        builder.Services.AddScoped<ISavedAutomatonService, SavedAutomatonService>();

        // register shared automaton services
        builder.Services.AddScoped<ISharedAutomatonService, SharedAutomatonService>();
        builder.Services.AddScoped<ISharedAutomatonSharingService, SharedAutomatonSharingService>();
        builder.Services.AddScoped<IInvitationNotificationService, InvitationNotificationService>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IExecuteService, ExecuteService>();

        // register concrete generator and decorated interface
        services.AddScoped<AutomatonGeneratorService>();
        services.AddScoped<IAutomatonGeneratorService>(sp =>
        {
            var inner = sp.GetRequiredService<AutomatonGeneratorService>();
            var audit = sp.GetRequiredService<IAuditService>();
            return new AutomatonGeneratorServiceAuditorDecorator(inner, audit);
        });

        services.AddScoped<IAutomatonTempDataService, AutomatonTempDataService>();
        services.AddScoped<IHomeAutomatonService, HomeAutomatonService>();
        services.AddScoped<IAutomatonValidationService, AutomatonValidationService>();

        // register concrete conversion and decorated interface
        services.AddScoped<AutomatonConversionService>();
        services.AddScoped<IAutomatonConversionService>(sp =>
        {
            var inner = sp.GetRequiredService<AutomatonConversionService>();
            var audit = sp.GetRequiredService<IAuditService>();
            return new AutomatonConversionServiceAuditorDecorator(inner, audit);
        });

        // register concrete execution service and decorator
        services.AddScoped<AutomatonExecutionService>();
        services.AddScoped<IAutomatonExecutionService>(sp =>
        {
            var inner = sp.GetRequiredService<AutomatonExecutionService>();
            var audit = sp.GetRequiredService<IAuditService>();
            return new AutomatonExecutionServiceAuditorDecorator(inner, audit);
        });

        services.AddScoped<IAutomatonBuilderService, AutomatonBuilderService>();
        services.AddScoped<IAutomatonEditingService, AutomatonEditingService>();
        services.AddScoped<IAutomatonFileService, AutomatonFileService>();
        services.AddScoped<IAutomatonMinimizationService, AutomatonMinimizationService>();
        services.AddScoped<IAutomatonAnalysisService, AutomatonAnalysisService>();
        services.AddScoped<IAutomatonPresetService, AutomatonPresetService>();
        services.AddScoped<IInputGenerationService, InputGenerationService>();

        // Register automaton types
        services.AddTransient<DFA>();
        services.AddTransient<NFA>();
        services.AddTransient<EpsilonNFA>();

        // Register regex services
        services.AddScoped<IRegexToAutomatonService, RegexToAutomatonService>();
        services.AddSingleton<IRegexPresetService, RegexPresetService>();
    }

    private static void ConfigureRequestPipeline(WebApplication app)
    {
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
                    var accept = context.Request.Headers.Accept.ToString();
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

        // Normalize incoming epsilon-like values (replacement char) to internal representation
        app.UseMiddleware<EpsilonNormalizationMiddleware>();

        // Ensure authentication middleware is registered so [Authorize] works correctly
        app.UseAuthentication();

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

            // New endpoint: build automaton from regex (development/testing only)
            app.MapPost("/_tests/build-from-regex", async (HttpRequest req, IRegexToAutomatonService regexService) =>
            {
                using var sr = new StreamReader(req.Body);
                var body = await sr.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body)) return Results.BadRequest("Empty body, expected raw regex string");
                try
                {
                    var enfa = regexService.BuildEpsilonNfaFromRegex(body.Trim());
                    // return a simple JSON describer of states and transitions
                    var payload = new
                    {
                        States = enfa.States.Select(s => new { s.Id, s.IsStart, s.IsAccepting }),
                        Transitions = enfa.Transitions.Select(t => new { t.FromStateId, t.ToStateId, Symbol = t.Symbol == '\0' ? "ε" : t.Symbol.ToString() })
                    };
                    return Results.Json(payload);
                }
                catch (Exception ex)
                {
                    var logger = req.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(ex, "Failed to build automaton from regex");
                    return Results.BadRequest(ex.Message);
                }
            }).WithDisplayName("BuildFromRegex");
        }

        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        app.MapRazorPages()
           .WithStaticAssets();
    }
}