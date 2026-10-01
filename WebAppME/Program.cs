using AuthService.Middleware;
using BatteryTestingSystem.Components;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Config;
using BatteryTestingSystem.Controllers;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Extensions;
using BatteryTestingSystem.MCP;
using BatteryTestingSystem.Models;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Implementations;
using BatteryTestingSystem.Utils;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
// ============================================================
// 1. LOGGING CONFIGURATION
// ============================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(InMemoryLogStore.LevelSwitch)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/btsservice-.log",            // base file name
        rollingInterval: RollingInterval.Day,    // new file every day
        retainedFileCountLimit: 7,               // keep only the last 7 files
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Sink(new InMemorySink())
    .CreateLogger();

// ✅ Required for SQLCipher
//Batteries_V2.Init();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting BTS Service");

    // ============================================================
    // 2. CONFIGURATION
    // ============================================================
    GlobalConfig.AppSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new();    //    ?? throw new InvalidOperationException("JwtSettings not configured");

    Directory.CreateDirectory(GlobalConfig.AppSettings.Data);

    // ============================================================
    // 3. SERVICES REGISTRATION
    // ============================================================


    // Add services to the container.
    builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

    // Blazor Method for circuit reconnect try 
     builder.Services
    .AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromSeconds(60);
    });

    // Custom dependencies
    builder.Services.AddBtsServiceDependencies(builder.Configuration);

    // Hangfire — uses same SQLite DB, separate file to avoid locking
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSQLiteStorage(Path.Combine(GlobalConfig.AppSettings.Data, "BtsHangfire.db")));
    builder.Services.AddHangfireServer();

    builder.Services.AddRazorPages(); // cshtml
    builder.Services.AddControllers();

    builder.Services.AddMcpServer()
       .WithHttpTransport(o => o.Stateless = true)
    .WithTools<DeviceMcpTools>();

    // CORS
    builder.Services.AddCorsPolicy(builder.Configuration);

    // Swagger
    builder.Services.AddSwaggerDocumentation();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>()
        // Surfaces TCP 9999 / UDP 10000 / UDP 10001 - without this the app can report
        // healthy while the hardware ports are down.
        .AddCheck<ListenerHealthCheck>("hardware-listeners");


    // handle circular references
    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    }).AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    });

    // ============================================================
    // 4. BUILD APPLICATION
    // ============================================================

    var app = builder.Build();

    ServiceLocator.SetProvider(app.Services);

    // ============================================================
    // 5. DATABASE MIGRATION & SEEDING
    // ============================================================
    try
    {
        using var dbContext = ServiceLocator.GetScoped<AppDbContext>();
        Log.Information("Applying database migrations...");
        dbContext.Service?.Database.Migrate();
        app.MigrateWorkflowCanvas();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating the database");
    }
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
    }

    // CORS
    app.UseCors();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // Hangfire Dashboard — only accessible to authenticated users (admin role recommended)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthFilter() }
    });
    app.MapRazorPages();
    app.MapControllers();
    app.MapMcp("/mcp");   // MCP at /mcp

    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();


    // ============================================================
    // 6. MIDDLEWARE PIPELINE
    // ============================================================

    // Custom exception handler (must be first)
    //app.UseCustomExceptionHandler();

    // Request logging
    //app.UseRequestLogging();


    // Swagger (Development only)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BTS Service API v1");
            c.RoutePrefix = "swagger";
        });
    }

    // ============================================================
    // 7. RUN APPLICATION
    // ============================================================
    Log.Information("BTS Service started successfully");
    Log.Information("Listening on: {Urls}", (app.Urls != null && app.Urls.Count == 0) ? "http://localhost:5000" : string.Join(", ", app.Urls));

    // For Get CurrentUserName
    CurrentUser.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        await InitializeDataSeeder.DefaultAuthAsync(services);

        await InitializeDataSeeder.DefaultRegistrationStandard(services);

        await ErrorMessages.GetMessages(true);

        await ErrorMessages.GetErrors(true);
    }

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("BTS Service shutting down");
    Log.CloseAndFlush();
}

// Make the implicit Program class public for testing
public partial class Program { }
