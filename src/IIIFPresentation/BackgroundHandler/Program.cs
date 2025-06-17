using AWS.Settings;
using BackgroundHandler.Helpers;
using BackgroundHandler.Infrastructure;
using BackgroundHandler.Settings;
using Core.Web;
using DLCS;
using Repository.Paths;
using Serilog;

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
var typedPathTemplateOptions = builder.Configuration.GetSection(TypedPathTemplateOptions.SettingsName);
builder.Services.Configure<TypedPathTemplateOptions>(typedPathTemplateOptions);

var aws = builder.Configuration.GetSection(AWSSettings.SettingsName).Get<AWSSettings>() ?? new AWSSettings();
var dlcsSettings = builder.Configuration.GetSection(DlcsSettings.SettingsName);
var dlcs = dlcsSettings.Get<DlcsSettings>()!;
    
builder.Services.AddAws(builder.Configuration, builder.Environment)
    .AddDataAccess(builder.Configuration)
    .AddDlcsOrchestratorClient(dlcs)
    .AddBackgroundServices(aws)
    .AddSingleton<IPathGenerator, SettingsBasedPathGenerator>()
    .AddSingleton<IPresentationPathGenerator, SettingsDrivenPresentationConfigGenerator>()
    .AddSingleton<IManifestMerger, ManifestMerger>()
    .Configure<DlcsSettings>(dlcsSettings);

var app = builder.Build();

app.Run();
