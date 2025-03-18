using System.Text.Json.Serialization;
using API.Auth;
using API.Features.Manifest;
using API.Helpers;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.Http.CorrelationId;
using API.Infrastructure.Http.Redirect;
using API.Settings;
using DLCS;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using Newtonsoft.Json;
using Repository;
using Repository.Manifests;
using Repository.Paths;
using Serilog;

const string corsPolicyName = "CorsPolicy";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
Log.Information("Application starting...");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((hostContext, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(hostContext.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId());

builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddOptions<ApiSettings>()
    .BindConfiguration(string.Empty);
builder.Services.AddOptions<CacheSettings>()
    .BindConfiguration(nameof(CacheSettings));
var dlcsSettings = builder.Configuration.GetSection(DlcsSettings.SettingsName);
builder.Services.Configure<DlcsSettings>(dlcsSettings);

var cacheSettings = builder.Configuration.GetSection(nameof(CacheSettings)).Get<CacheSettings>() ?? new CacheSettings();
var dlcs = dlcsSettings.Get<DlcsSettings>()!;

builder.Services
    .AddDlcsApiClient(dlcs)
    .AddDelegatedAuthHandler(opts => { opts.Realm = "DLCS-API"; });
builder.Services.ConfigureDefaultCors(corsPolicyName);
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddCaching(cacheSettings);
builder.Services
    .ConfigureSwagger()
    .AddSingleton<IETagManager, ETagManager>()
    .AddScoped<IManifestWrite, ManifestWriteService>()
    .AddScoped<DlcsManifestCoordinator>()
    .AddScoped<IManifestRead, ManifestReadService>()
    .AddScoped<CanvasPaintingResolver>()
    .AddSingleton<ManifestItemsParser>()
    .AddSingleton<IPathGenerator, HttpRequestBasedPathGenerator>()
    .AddScoped<IParentSlugParser, ParentSlugParser>()
    .AddHttpContextAccessor()
    .AddOutgoingHeaders();
builder.Services.ConfigureMediatR();
builder.Services.ConfigureIdGenerator();
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<PresentationContext>("Database");
builder.Services.AddAws(builder.Configuration, builder.Environment);
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOptionsWithValidateOnStart<Program>();

var app = builder.Build();

app
    .UseMiddleware<CorrelationIdMiddleware>()
    .UseForwardedHeaders();

IIIFPresentationContextConfiguration.TryRunMigrations(builder.Configuration, app.Logger);

var rewriteOptions = new RewriteOptions()
    .Add(new TrailingSlashRedirectRule());

app
    .UseSwagger()
    .UseSwaggerUI()
    .UseRewriter(rewriteOptions)
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization()
    .UseSerilogRequestLogging()
    .UseCors(corsPolicyName);

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
