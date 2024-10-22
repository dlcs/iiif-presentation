using System.Text.Json.Serialization;
using API.Auth;
using API.Features.Storage.Validators;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Newtonsoft.Json;
using Repository;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog(lc => lc
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
});

builder.Services.AddScoped<PresentationCollectionValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<ApiSettings>()
    .BindConfiguration(string.Empty);
builder.Services.AddOptions<CacheSettings>()
    .BindConfiguration(nameof(CacheSettings));
var dlcsSettings = builder.Configuration.GetSection(DlcsSettings.SettingsName);
builder.Services.Configure<DlcsSettings>(dlcsSettings);

var cacheSettings = builder.Configuration.GetSection(nameof(CacheSettings)).Get<CacheSettings>() ?? new CacheSettings();
var dlcs = dlcsSettings.Get<DlcsSettings>()!;

builder.Services.AddDelegatedAuthHandler(dlcs, opts =>
{
    opts.Realm = "DLCS-API";
});
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddCaching(cacheSettings);
builder.Services.AddSingleton<IETagManager, ETagManager>();
builder.Services.ConfigureMediatR();
builder.Services.ConfigureIdGenerator();
builder.Services.AddHealthChecks();
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

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    IIIFPresentationContextConfiguration.TryRunMigrations(builder.Configuration, app.Logger);
}

app
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization()
    .UseSerilogRequestLogging();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }