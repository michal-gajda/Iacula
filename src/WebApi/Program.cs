namespace Iacula.WebApi;

using Iacula.Application;
using Iacula.Infrastructure;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public class Program
{
    private const string SERVICE_NAME = "webapi";
    private const string SERVICE_NAMESPACE = "iacula";
    private const string SERVICE_VERSION = "poc";

    public static async Task Main(string[] args)
    {
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName: SERVICE_NAME, serviceVersion: SERVICE_VERSION)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("service.namespace", SERVICE_NAMESPACE),
                new("service.instance.id", Environment.MachineName)
            });

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);


        builder.Logging.AddOpenTelemetry(options => options.SetResourceBuilder(resourceBuilder).AddOtlpExporter());

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
                .AddHttpClientInstrumentation(opt => opt.RecordException = true)
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
                ;

        builder.Services.AddHealthChecks();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddAuthorization();

        var app = builder.Build();

        await app.InitializeInfrastructureAsync();

        app.UseHealthChecks("/healthz");

        app.MapReverseProxy();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Iacula API v1");
            });
        }

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();

        app.MapControllers();

        app.UseAuthorization();

        app.MapFallbackToFile("index.html");

        await app.RunAsync();
    }
}
