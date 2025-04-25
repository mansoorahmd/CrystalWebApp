using CrystalWebApp;
using Microsoft.Extensions.Options;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Crystal Web API",
        Version = "v1",
        Description = "API for Crystal Reports"
    });
    c.EnableAnnotations();
    c.OperationFilter<RequestBodyDefaultValueFilter>();
});
builder.Services.AddTransient<CrystalWebApp.Utilities.SqlDataService>();

var app = builder.Build();

// Enable Serilog request logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Crystal Web API v1");
    });
}

app.UseAuthorization();

app.MapControllers();

// Create the logs directory if it doesn't exist
var logDir = Path.Combine(app.Environment.ContentRootPath, "logs");
if (!Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}

// Log application startup
Log.Information("Starting Crystal Web API");

app.Run();

// Clean up Serilog when app is shutting down
app.Lifetime.ApplicationStopped.Register(() => Log.CloseAndFlush());
