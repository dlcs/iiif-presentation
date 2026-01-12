using AWS.Settings;
using BackgroundHandler.Infrastructure;
using BackgroundHandler.Settings;
using Core.Web;
using DLCS;
using Repository.Paths;
using Serilog;
using Services.Manifests;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
Log.Information("Application starting...");

builder.Host.UseSerilog((hostContext, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(hostContext.Configuration)
        .Enrich.FromLogContext());

builder.Services.AddOptions<BackgroundHandlerSettings>()
    .BindConfiguration(string.Empty);
var pathSettings = builder.Configuration.GetSection(PathSettings.SettingsName);
builder.Services.Configure<PathSettings>(pathSettings);
var typedPathTemplateOptions = pathSettings.GetSection(TypedPathTemplateOptions.SettingsName);
builder.Services.Configure<TypedPathTemplateOptions>(typedPathTemplateOptions);

var aws = builder.Configuration.GetSection(AWSSettings.SettingsName).Get<AWSSettings>() ?? new AWSSettings();
var dlcsSettings = builder.Configuration.GetSection(DlcsSettings.SettingsName);
var dlcs = dlcsSettings.Get<DlcsSettings>()!;
    
builder.Services.AddAws(builder.Configuration, builder.Environment)
    .AddDataAccess(builder.Configuration)
    .AddDlcsOrchestratorClient(dlcs)
    .AddBackgroundServices(aws)
    .AddSingleton<IPathGenerator, SettingsBasedPathGenerator>()
    .AddSingleton<SettingsBasedPathGenerator>()
    .AddSingleton<SettingsDrivenPresentationConfigGenerator>()
    .AddSingleton<IPresentationPathGenerator, SettingsDrivenPresentationConfigGenerator>()
    .AddSingleton<IPathRewriteParser, PathRewriteParser>()
    .AddSingleton<IManifestMerger, ManifestMerger>()
    .AddSingleton<IManifestStorageManager, ManifestS3Manager>()
    .Configure<DlcsSettings>(dlcsSettings);

var app = builder.Build();

app.Run();
