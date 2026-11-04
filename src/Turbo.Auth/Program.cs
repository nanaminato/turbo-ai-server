using Turbo.Auth.Security.FeatureAccess;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Turbo.Auth.Security.PasswordReset;
using Turbo.Auth.Bootstrap;
using Turbo.Auth.Localization;
using Microsoft.AspNetCore.Localization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Turbo.Auth.Data.Contexts;
using Turbo.Auth.Controllers.Files;
using Turbo.Auth.Controllers.Sync;
using Turbo.Auth.Application.Routing;
using Turbo.Auth.Application.Chat;
using Turbo.Auth.Models.Config;
using Turbo.Auth.Models.Accounts;
using Turbo.Auth.Options;
using Turbo.Auth.Repositories.Accounts;
using Turbo.Auth.Repositories.Catalog;
using Turbo.Auth.Repositories.Sync;
using Turbo.Auth.Security;
using Turbo.Kit.Pdf;
using Turbo.Kit.Text;
using Turbo.Kit.Word;

if (await DatabaseSetupCommand.TryRunAsync(args)) return;

if (args.Length == 1 && string.Equals(args[0], "--hash-password", StringComparison.Ordinal))
{
    if (Console.IsInputRedirected)
    {
        Console.Error.WriteLine("请在交互式终端中运行 --hash-password，以避免通过命令行或管道暴露密码。");
        Environment.ExitCode = 1;
        return;
    }

    Console.Write("请输入密码: ");
    var password = ReadPassword();
    Console.WriteLine();

    if (string.IsNullOrWhiteSpace(password))
    {
        Console.Error.WriteLine("密码不能为空。");
        Environment.ExitCode = 1;
        return;
    }

    var passwordHash = new AccountPasswordService().Hash(new Account(), password);
    Console.WriteLine(passwordHash);
    return;
}

var builder = WebApplication.CreateBuilder(args);
var allowAnyCorsOrigin = builder.Environment.IsDevelopment();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
    .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

builder.Services.AddCors(options => options.AddPolicy("CorsPolicy",
    set =>
    {
        if (allowAnyCorsOrigin)
        {
            // Browsers disallow combining a wildcard origin with credentials.
            // Local development uses bearer tokens, so no credentialed CORS is needed.
            set.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else if (allowedOrigins.Length == 0)
        {
            set.SetIsOriginAllowed(_ => false);
        }
        else
        {
            set.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    }));
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("zh");
    options.AddSupportedCultures("zh", "en", "ja");
    options.AddSupportedUICultures("zh", "en", "ja");
    // Only the explicit request header controls API response language.
    options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
    options.ApplyCurrentCultureToResponseHeaders = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
});

builder.Services.AddOptions<PasswordResetOptions>().Bind(builder.Configuration.GetSection("PasswordReset"))
    .Validate(options => options.IsValid(), "PasswordReset configuration is invalid.").ValidateOnStart();
var protection = builder.Services.AddDataProtection().SetApplicationName("Turbo.Auth");
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddSingleton<ResetTokenService>();
builder.Services.AddScoped<IPasswordResetStore, PasswordResetStore>();
builder.Services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("password-reset", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});
builder.Services.AddSignalR();
Turbo.Auth.Configuration.ApiDocumentation.AddApiDocumentation(builder.Services);
var connectionString = builder.Configuration.GetConnectionString("ciko");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:ciko must be configured through environment-specific configuration or a secret store.");
}

var serverVersion = new MySqlServerVersion(new Version(8, 4, 6));
var enableSensitiveDatabaseLogging = builder.Environment.IsDevelopment() &&
                                     builder.Configuration.GetValue<bool>("Diagnostics:EnableSensitiveDataLogging");

void ConfigureDatabase(DbContextOptionsBuilder options)
{
    options.UseMySql(connectionString, serverVersion)
        .LogTo(Console.WriteLine, LogLevel.Error);

    if (enableSensitiveDatabaseLogging)
    {
        options.EnableSensitiveDataLogging()
            .EnableDetailedErrors();
    }
}

builder.Services.AddDbContext<AuthContext>(
    ConfigureDatabase
);
builder.Services.AddDbContext<KeyContext>(
    ConfigureDatabase
);
builder.Services.AddScoped<FeatureIdentity>();
builder.Services.AddSingleton<FeatureAccessStore>();
builder.Services.AddMemoryCache(); // 添加内存缓存支持
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("AiProvider", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<IIdGetter, IdGetter>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountRoleRepository, AccountRoleRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IKeyRepository, KeyRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IFileContentExtractor, FileContentExtractor>();
builder.Services.AddScoped<IPdfDocumentProcessor, PdfDocumentProcessor>();
builder.Services.AddScoped<IWordDocumentProcessor, WordDocumentProcessor>();
builder.Services.AddScoped<ITextDocumentProcessor, TextDocumentProcessor>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RouteReloadGate>();
builder.Services.AddScoped<IKeyLoader, KeyLoader>();
builder.Services.AddHostedService<RoutingRefreshService>();
builder.Services.AddScoped<IKeyPoolRepository, StableKeyPoolRepository>();
builder.Services.Configure<AiRoutingOptions>(builder.Configuration.GetSection("AiRouting"));
builder.Services.AddSingleton<IRouteHealthTracker, RouteHealthTracker>();
builder.Services.AddSingleton<QuickModel>();
builder.Services.AddSingleton<PlayMixModelBacker>();
builder.Services.AddScoped<IModelKeyBuilder, ModelKeyBuilder>();
builder.Services.AddScoped<IChatHandler, OpenAiChatHandler>();
builder.Services.AddScoped<IChatHandler, GoogleChatHandler>();
builder.Services.AddScoped<IChatHandler, AnthropicChatHandler>();
builder.Services.AddScoped<IChatHandler, AlibabaChatHandler>();
builder.Services.AddScoped<IChatHandlerObtain, ChatHandlerObtain>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<Turbo.Auth.Application.Media.IMediaService, Turbo.Auth.Application.Media.MediaService>();
builder.Services.AddScoped<Turbo.Auth.Application.Providers.OpenAiClientFactory>();
builder.Services.AddScoped<Turbo.Auth.Application.Sync.DataSyncService>();
builder.Services.AddSingleton<IAccountPasswordService, AccountPasswordService>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

var jswSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt settings are required.");
if (string.IsNullOrWhiteSpace(jswSettings.SecretKey) ||
    string.IsNullOrWhiteSpace(jswSettings.Issuer) ||
    string.IsNullOrWhiteSpace(jswSettings.Audience) ||
    string.IsNullOrWhiteSpace(jswSettings.RefreshTokenPepper))
{
    throw new InvalidOperationException("Jwt:Issuer, Jwt:Audience, Jwt:SecretKey, and Jwt:RefreshTokenPepper must be configured through environment-specific configuration or a secret store.");
}

if (jswSettings.AccessTokenMinutes is < 10 or > 15)
{
    throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 10 and 15.");
}

if (jswSettings.RefreshTokenDays is < 1 or > 30)
{
    throw new InvalidOperationException("Jwt:RefreshTokenDays must be between 1 and 30.");
}

var secretKey = Encoding.UTF8.GetBytes(jswSettings.SecretKey);
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jswSettings.Issuer,
            ValidAudience = jswSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var accountIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var securityStamp = context.Principal?.FindFirst("sv")?.Value;
                var sessionIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value;
                if (!int.TryParse(accountIdValue, out var accountId) ||
                    string.IsNullOrWhiteSpace(securityStamp) ||
                    !Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    context.Fail("The access token does not contain a valid session.");
                    return;
                }

                var sessions = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionService>();
                if (!await sessions.IsAccessTokenValidAsync(accountId, securityStamp, sessionId,
                        context.HttpContext.RequestAborted))
                {
                    context.Fail("The session has been revoked.");
                    return;
                }
                // JWT roles are historical claims: replace them with current persisted roles.
                if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray()) identity.RemoveClaim(claim);
                    var resolver = context.HttpContext.RequestServices.GetRequiredService<FeatureIdentity>();
                    foreach (var role in await resolver.Roles(accountId, context.HttpContext.RequestAborted))
                        identity.AddClaim(new System.Security.Claims.Claim(identity.RoleClaimType, role));
                }
            }
        };
    });
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>,
        ConfigureJwtBearerOptions>());
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin", policy => policy.RequireRole(["admin"]));
    options.AddPolicy("user", policy => policy.RequireRole(["user"]));
    options.AddPolicy("vip", policy => policy.RequireRole(["vip"]));
});
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (builder.Environment.IsDevelopment() || string.IsNullOrEmpty(redisConnection))
{
    // 开发环境或没配 Redis 时，使用本地内存模拟
    builder.Services.AddDistributedMemoryCache();
}
else
{
    // 生产环境使用 Redis
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "ApiProxy_";
    });
}

var app = builder.Build();
app.UseRequestLocalization();
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("UnhandledException");
    logger.LogError(
        "Unhandled exception. TraceId: {TraceId}; ExceptionType: {ExceptionType}",
        context.TraceIdentifier,
        exception?.GetType().Name);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = ApiMessages.Get("internal_error"),
        Extensions = { ["code"] = "internal_error", ["traceId"] = context.TraceIdentifier },
        Instance = context.Request.Path
    });
}));
app.UseCors("CorsPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FeatureAccessMiddleware>();
app.MapControllers();
app.MapFallbackToFile("index.html");


app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Urls;
    foreach (var address in addresses)
    {
        Console.WriteLine($"应用已真正启动，监听地址: {address}");
    }
});

app.Run();

static string ReadPassword()
{
    var password = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            return password.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Length--;
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }
}
