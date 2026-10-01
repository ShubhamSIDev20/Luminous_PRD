using BatteryTestingSystem.Components.UI.TabViewer;
using BatteryTestingSystem.Config;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.DbSecurity;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Implementations;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Auth;
using BatteryTestingSystem.Services.BROADCAST;
using BatteryTestingSystem.Services.Implementations;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BatteryTestingSystem.Extensions;
public static class ServiceCollectionExtensions
{
    // Shared RSA key - in production, load from secure storage
    private static RSA? _sharedRsaKey;

    private static RSA GetOrCreateRsaKey()
    {
        if (_sharedRsaKey == null)
        {
            _sharedRsaKey = RSA.Create(2048);
        }
        return _sharedRsaKey;
    }

    public static IServiceCollection AddBtsServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.AddScoped<CircuitHandler, MyCircuitHandler>();

        services.AddSingleton<IColumnEncryptionService>(
            new ColumnEncryptionService(GlobalConfig.AppSettings.DbEncryption));

        services.AddSingleton<IColumnEncryptionServiceWithSalt>(
            new ColumnEncryptionServiceWithSalt(GlobalConfig.AppSettings.DbEncryption));

        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        #region Repo
        services.AddScoped<IDeviceCircuitRepository, DeviceCircuitRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IBatteryRepository, BatteryRepository>();
        services.AddScoped<IConfigStorageRepository, ConfigStorageRepository>();
        services.AddScoped<ICodeMessageRepository, CodeMessageRepository>();
        services.AddScoped<IDbcRepository, DbcRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUserCircuitAccessRepository, UserCircuitAccessRepository>();
        //sqlite DB Operations
        services.AddScoped<ISqliteBulkDatabaseManager, SqliteBulkDatabaseManager>();

        #endregion

        #region Service
        services.AddSingleton<EventBusService>();

        services.AddSingleton<Func<ICircuitCommandHandler>>(sp =>
        {
            return () => new CircuitCommandHandler();
        });

        services.AddSingleton<CircuitManager>();

        services.AddHostedService(provider => provider.GetRequiredService<CircuitManager>());

        services.AddScoped<IAuditService, AuditService>();

        services.AddScoped<IProgramServices, ProgramServices>();
        services.AddScoped<IBatteryServices, BatteryServices>();
        services.AddScoped<IDeviceCircuitServices, DeviceCircuitServices>();
        services.AddScoped<ICodeMessageService, CodeMessageService>();
        services.AddScoped<IDbcService, DbcService>();
        services.AddScoped<IUserCircuitAccessService, UserCircuitAccessService>();

        // Scheduler
        services.AddScoped<ISchedulerRepository, SchedulerRepository>();
        services.AddScoped<ISchedulerService, SchedulerService>();

        //Broadcast UDP for Scope Discovery
        services.AddSingleton<BroadcastUdpService>();   // ← add this

        #endregion

        #region Session Storage
        services.AddScoped<IConfigStorageService, ConfigStorageService>();
        services.AddSingleton<ServerSessionStorageService>();
        #endregion

        #region Export (T009 — large-session async export)
        services.AddHttpClient();
        // Repository: scoped — one per HTTP request / Hangfire job scope
        services.AddScoped<IExportRepository, ExportRepository>();
        // Job service: transient — Hangfire resolves a new instance per job execution
        services.AddTransient<ExportJobService>();
        #endregion

        #region SessionID

        // SESSION CONFIGURATION - Add BEFORE Identity
        //services.AddDistributedMemoryCache();
        //services.AddSession(options =>
        //{
        //    options.IdleTimeout = TimeSpan.FromMinutes(30);
        //    options.Cookie.Name = ".BatteryTestingSystem.Session";
        //    options.Cookie.HttpOnly = true;
        //    options.Cookie.IsEssential = true;
        //    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // For HTTPS
        //});

        #endregion

        #region Identity

        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityUserAccessor>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();


        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/accessdenied"; // optional


            options.Events.OnSigningIn = context =>
            {
                var identity = (ClaimsIdentity)context.Principal.Identity!;

                if (!identity.HasClaim(c => c.Type == "sId"))
                {
                    identity.AddClaim(new Claim("sId", Guid.NewGuid().ToString("N")));
                }

                return Task.CompletedTask;
            };

            options.Events.OnValidatePrincipal = context =>
            {
                var identity = (ClaimsIdentity)context.Principal!.Identity!;

                if (!identity.HasClaim(c => c.Type == "sId"))
                {
                    identity.AddClaim(new Claim("sId", Guid.NewGuid().ToString("N")));
                }

                return Task.CompletedTask;
            };

            options.Events.OnSigningOut = context =>
            {

                //var identity = context.HttpContext.User.Identity as ClaimsIdentity;

                //var sId = identity?.FindFirst("sId")?.Value;

                //if (!string.IsNullOrEmpty(sId))
                //{
                //    var tracker = context.HttpContext.RequestServices
                //        .GetRequiredService<ServerSessionStorageService>();

                //    tracker.RemoveComponentState(sId);
                //}

                return Task.CompletedTask;
            };

        });


        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredUniqueChars = 1;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;

            // SignIn settings
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddRoleManager<RoleManager<ApplicationRole>>()
        .AddDefaultTokenProviders();

        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();


        services.AddHttpContextAccessor();

        #endregion

        #region Theme
        services.AddScoped<ToastService>();
        services.AddScoped<PopupService>();
        services.AddScoped<KeyboardService>();
        #endregion

        #region UI Services
        services.AddScoped<TabService>();
        #endregion

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtSettings jwtSettings)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
                        context.NoResult();

                    return Task.CompletedTask;
                },

                OnAuthenticationFailed = context =>
                {
                    var logger = Log.ForContext("SourceContext", "JwtAuthentication");
                    var request = context.HttpContext.Request;
                    var connection = context.HttpContext.Connection;

                    logger.Warning(context.Exception,
                        "JWT authentication failed | " +
                        "Reason: {Reason} | " +
                        "IP: {RemoteIp} | " +
                        "Path: {Path} | " +
                        "Method: {Method} | " +
                        "UserAgent: {UserAgent} | " +
                        "Referer: {Referer} | " +
                        "RequestId: {RequestId}",
                        context.Exception.GetType().Name,
                        connection.RemoteIpAddress?.ToString() ?? "unknown",
                        request.Path,
                        request.Method,
                        request.Headers.UserAgent.ToString(),
                        request.Headers.Referer.ToString(),
                        context.HttpContext.TraceIdentifier
                    );

                    if (context.Exception is SecurityTokenExpiredException)
                        context.Response.Headers.Add("Token-Expired", "true");

                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection("Cors").Get<CorsSettings>();

        if (corsSettings != null)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins(corsSettings.AllowedOrigins)
                        .WithMethods(corsSettings.AllowedMethods)
                        .WithHeaders(corsSettings.AllowedHeaders);

                    if (corsSettings.AllowCredentials)
                        builder.AllowCredentials();

                    builder.SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.MaxAgeSeconds));
                });
            });
        }

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "BTS Service API",
                Version = "v1",
                Description = "Authentication & Authorization Service",
                Contact = new OpenApiContact
                {
                    Name = "Auth Service",
                    Email = "support@authservice.com"
                }
            });

            // Add JWT authentication
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Include XML comments if available
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}

