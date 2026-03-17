namespace Iacula.Infrastructure.MassTransit;

using global::MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class ServiceExtensions
{
    public static void AddMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        var massTransitSection = configuration.GetSection("MassTransit");
        var hostOptions = massTransitSection.Get<MassTransitHostOptions>();
        hostOptions ??= new MassTransitHostOptions
        {
            WaitUntilStarted = true,
            StartTimeout = TimeSpan.FromSeconds(30),
            StopTimeout = TimeSpan.FromSeconds(30),
        };

        services.Configure<MassTransitHostOptions>(cfg =>
        {
            cfg.WaitUntilStarted = hostOptions.WaitUntilStarted;
            cfg.StartTimeout = hostOptions.StartTimeout;
            cfg.StopTimeout = hostOptions.StopTimeout;
            cfg.ConsumerStopTimeout = hostOptions.ConsumerStopTimeout;
        });

        var transportOptions = configuration.GetSection("RabbitMQ").Get<RabbitMqTransportOptions>();
        transportOptions ??= new RabbitMqTransportOptions
        {
            Host = "localhost",
            Pass = "guest",
            Port = 5672,
            User = "guest",
            UseSsl = false,
            VHost = "/",
        };

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(transportOptions.Host, transportOptions.Port, transportOptions.VHost, host =>
                {
                    host.Username(transportOptions.User);
                    host.Password(transportOptions.Pass);

                    if (transportOptions.UseSsl)
                    {
                        host.UseSsl();
                    }
                });

                cfg.PrefetchCount = 16;
                cfg.ConfigureEndpoints(context);
            });
        });

        // services.AddHostedService<Worker>();
    }
}
