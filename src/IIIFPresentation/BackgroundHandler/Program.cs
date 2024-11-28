using AWS.Settings;
using BackgroundHandler.Infrastructure;
using BackgroundHandler.Settings;
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
var aws = builder.Configuration.GetSection("AWS").Get<AWSSettings>() ?? new AWSSettings();
    
builder.Services.AddAws(builder.Configuration, builder.Environment);
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBackgroundServices(aws);

var app = builder.Build();

app.Run();