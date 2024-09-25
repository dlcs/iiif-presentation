using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using API.Features.Storage.Validators;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Settings;
using Core.Response;
using IIIF.Presentation.V3;
using Microsoft.AspNetCore.HttpOverrides;
using Models.API.Collection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

builder.Services.AddScoped<UpsertFlatCollectionValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<ApiSettings>()
    .BindConfiguration(nameof(ApiSettings));
builder.Services.AddOptions<CacheSettings>()
    .BindConfiguration(nameof(CacheSettings));

var cacheSettings = builder.Configuration.Get<CacheSettings>() ?? new CacheSettings();

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddCaching(cacheSettings);
builder.Services.AddSingleton<IETagManager, ETagManager>();
builder.Services.ConfigureMediatR();
builder.Services.AddSingleton<ETagManager>();
builder.Services.AddHealthChecks();
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

app.UseHttpsRedirection();

app.UseSerilogRequestLogging();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }