using AWS.Settings;
using BackgroundHandler.Infrastructure;
using BackgroundHandler.Settings;
using DLCS;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog(lc => lc
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddOptions<BackgroundHandlerSettings>()
    .BindConfiguration(string.Empty);
var aws = builder.Configuration.GetSection(AWSSettings.SettingsName).Get<AWSSettings>() ?? new AWSSettings();
var dlcsSettings = builder.Configuration.GetSection(DlcsSettings.SettingsName);
var dlcs = dlcsSettings.Get<DlcsSettings>()!;
    
builder.Services.AddAws(builder.Configuration, builder.Environment)
    .AddDataAccess(builder.Configuration)
    .AddHttpContextAccessor()
    .AddDlcsClientWithLocalAuth(dlcs)
    .AddBackgroundServices(aws)
    .Configure<DlcsSettings>(dlcsSettings);

var app = builder.Build();

app.Run();
