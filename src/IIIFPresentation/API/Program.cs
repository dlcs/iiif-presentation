using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using API.Features.Storage.Validators;
using API.Infrastructure;
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

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.ConfigureMediatR();
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

#pragma warning disable SYSLIB0050
HttpResponseMessageX.SerializationContext = new StreamingContext(StreamingContextStates.All,
    new Dictionary<string, Func<JObject, ResourceBase>>
    {
        [nameof(FlatCollection)] = _ => new FlatCollection {Slug = string.Empty}
    });
#pragma warning restore SYSLIB0050

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