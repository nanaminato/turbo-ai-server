using System.Text;
using DotnetGeminiSDK;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Turbo_Auth.Context;
using Turbo_Auth.Controllers.Extractors;
using Turbo_Auth.Controllers.MessageSync;
using Turbo_Auth.Handlers.Builder;
using Turbo_Auth.Handlers.Chat;
using Turbo_Auth.Handlers.keyPool;
using Turbo_Auth.Handlers.Loader;
using Turbo_Auth.Handlers.Model;
using Turbo_Auth.Handlers.Model2Key;
using Turbo_Auth.Models.Config;
using Turbo_Auth.Options;
using Turbo_Auth.Repositories.Accounts;
using Turbo_Auth.Repositories.ApiAssets;
using Turbo_Auth.Repositories.Messages;
using Turbo_Auth.Repositories.Novita;
using Turbo_Kit.PDF;
using Turbo_Kit.Text;
using Turbo_Kit.WORD;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddPolicy("CorsPolicy",
    set =>
    {
        set.SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
});

builder.Services.AddSignalR();
builder.Services.AddSwaggerGen();
var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));

// Replace 'YourDbContext' with the name of your own DbContext derived class.
builder.Services.AddDbContext<AuthContext>(
    dbContextOptions =>
    {
        dbContextOptions
            .UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"), serverVersion)
            .LogTo(Console.WriteLine, LogLevel.Error)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors();
    }
);
builder.Services.AddDbContext<KeyContext>(
    dbContextOptions =>
    {
        dbContextOptions
            .UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"), serverVersion)
            .LogTo(Console.WriteLine, LogLevel.Error)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors();
    }
);
builder.Services.AddGeminiClient(options =>
{
    options.ApiKey = "AIzaSyDW98j3Qe1nXWCq-6wGxAQfIZKCpD7zpa4";
});
builder.Services.AddScoped<IIdGetter, IdGetter>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountRoleRepository,AccountRoleRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IKeyRepository, KeyRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IFileContentExtractor, FileContentExtractor>();
builder.Services.AddScoped<IPdfDocumentProcessor, PdfDocumentProcessor>();
builder.Services.AddScoped<IWordDocumentProcessor, WordDocumentProcessor>();
builder.Services.AddScoped<ITextDocumentProcessor, TextDocumentProcessor>();
builder.Services.AddScoped<INovitaModelRepository, NovitaModelRepository>();
builder.Services.AddScoped<IKeyLoader, KeyLoader>();
builder.Services.AddScoped<IKeyPoolRepository, StableKeyPoolRepository>();
builder.Services.AddSingleton<QuickModel>();
builder.Services.AddSingleton<PlayMixModelBacker>();
builder.Services.AddScoped<IModelKeyBuilder, ModelKeyBuilder>();
builder.Services.AddScoped<IChatHandlerObtain, ChatHandlerObtain>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));


var jswSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
var secretKey = Encoding.UTF8.GetBytes(jswSettings!.SecretKey!);
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
        options.Events = new JwtBearerEvents();
    });
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>,
        ConfigureJwtBearerOptions>());

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin", policy => policy.RequireRole(["admin"]));
    options.AddPolicy("user", policy => policy.RequireRole(["user"]));
    options.AddPolicy("vip", policy => policy.RequireRole(["vip"]));
});
// builder.WebHost.ConfigureKestrel(options =>
// {
//     var configuration = builder.Configuration;
//     if (configuration["Azure"] != "NOT") return;
//     var port = Int32.Parse(configuration["Port"]??"9000");
//     options.ListenAnyIP(port); // 监听所有 IP 的 5001 端口
// });
var app = builder.Build();
app.UseCors("CorsPolicy");
app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapFallbackToFile("/ai/{*path:nonfile}", "ai/index.html");
app.MapFallbackToFile("/admin/{*path:nonfile}", "admin/index.html");

app.MapControllers();
var serviceProvider = app.Services.CreateScope().ServiceProvider;
var loader = serviceProvider.GetRequiredService<IKeyLoader>();
loader.LoadKeys();
app.Run();