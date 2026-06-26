using AWS.Settings;
using BackgroundHandler.Infrastructure;
using BackgroundHandler.Settings;
using Core.Settings;
using DLCS;
using Repository.Helpers;
using Repository.Paths;
using Serilog;
using Services;
using Services.Manifests;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;

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

var aws = builder.Configuration.GetSection(AWSSettings.SettingsName).Get<AWSSettings>() ?? new AWSSettings();
var dlcsSettings = builder.Configuration.GetSection(DlcsSettings.SettingsName);
var dlcs = dlcsSettings.Get<DlcsSettings>()!;

var textServicesSettings = builder.Configuration.GetSection(TextServicesSettings.SettingsName);
builder.Services.Configure<TextServicesSettings>(textServicesSettings);
var textServices = textServicesSettings.Get<TextServicesSettings>() ?? new TextServicesSettings();

builder.RegisterSharedServiceSettings();
    
builder.Services.AddAws(builder.Configuration, builder.Environment)
    .AddDataAccess(builder.Configuration)
    .AddDlcsOrchestratorClient(dlcs)
    .AddTextBuilderClient(textServices)
    .AddTextSearchClient(textServices)
    .AddBackgroundServices(aws)
    .AddSingleton<IPathGenerator, SettingsBasedPathGenerator>()
    .AddSingleton<SettingsBasedPathGenerator>()
    .AddSingleton<SettingsDrivenPresentationConfigGenerator>()
    .AddSingleton<IPresentationPathGenerator, SettingsDrivenPresentationConfigGenerator>()
    .AddSingleton<IPathRewriteParser, PathRewriteParser>()
    .AddScoped<IManifestMerger, ManifestMerger>()
    .AddScoped<IDlcsManifestMerger, DlcsManifestMerger>()
    .AddScoped<IManifestStorageManager, ManifestS3Manager>()
    .AddScoped<ICustomerIdProvider, SetCustomerIdProvider>()
    .Configure<DlcsSettings>(dlcsSettings);

var app = builder.Build();

app.Run();
