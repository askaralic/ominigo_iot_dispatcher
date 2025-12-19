using Serilog;
using Serilog.Events;
using TelemetryDBBridgeService.HostedServices;
using TelemetryDBBridgeService.Options;
using TelemetryDBBridgeService.Services;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseSerilog((context, services, configuration) =>
    {
        var logPath = Path.Combine("Logs", "log-.txt");
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<WorkerOptions>(context.Configuration.GetSection("Worker"));
        services.Configure<ApiOptions>(context.Configuration.GetSection("Api"));
        services.Configure<MongoOptions>(context.Configuration.GetSection("Mongo"));

        services.AddHttpClient<ApiClient>();

        services.AddSingleton<MongoRepository>();
        services.AddHostedService<QueuePullerHostedService>();
        services.AddHostedService<DispatcherHostedService>();
    });

var host = builder.Build();
await host.RunAsync();
