using System.Text.Json.Serialization;
using API.Infrastructure;
using API.Infrastructure.Validation;
using API.Settings;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Repository;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog(lc => lc
    .WriteTo.Console()
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<ApiSettings>()
    .BindConfiguration(nameof(ApiSettings));

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.ConfigureMediatR();

builder.Services.ConfigureHttpJsonOptions( options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOptionsWithValidateOnStart<Program>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    IiifPresentationContextConfiguration.TryRunMigrations(builder.Configuration, app.Logger);
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program { }