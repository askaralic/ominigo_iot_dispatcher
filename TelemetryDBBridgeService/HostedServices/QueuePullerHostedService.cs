using Microsoft.Extensions.Options;
using TelemetryDBBridgeService.Options;
using TelemetryDBBridgeService.Services;
using TelemetryDBBridgeService.Models;

namespace TelemetryDBBridgeService.HostedServices;

public class QueuePullerHostedService : BackgroundService
{
    private readonly WorkerOptions _workerOptions;
    private readonly ApiClient _apiClient;
    private readonly MongoRepository _mongoRepository;
    private readonly ILogger<QueuePullerHostedService> _logger;

    public QueuePullerHostedService(
        IOptions<WorkerOptions> workerOptions,
        ApiClient apiClient,
        MongoRepository mongoRepository,
        ILogger<QueuePullerHostedService> logger)
    {
        _workerOptions = workerOptions.Value;
        _apiClient = apiClient;
        _mongoRepository = mongoRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue puller starting with poll interval {Seconds}s", Math.Max(1, _workerOptions.PollIntervalSeconds));

        if (!_workerOptions.Enabled)
        {
            _logger.LogInformation("Worker disabled via configuration. Queue puller will not start.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, _workerOptions.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PullQueueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogWarning("Queue puller operation canceled; continuing loop.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while pulling queue.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PullQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Queue puller fetching batch of size {BatchSize}", _workerOptions.BatchSize);
        var items = await _apiClient.GetQueueAsync(_workerOptions.BatchSize, _workerOptions.InstanceId, cancellationToken);

        if (items.Count == 0)
        {
            _logger.LogDebug("No queue items returned from API.");
            return;
        }

        foreach (var item in items)
        {
            try
            {
                await _mongoRepository.UpsertQueueItemAsync(item, cancellationToken);
                _logger.LogInformation("Upserted dispatch_queue_uno {DispatchQueueUno}", item.DispatchQueueUno);

                var inProgressUpdate = new UpdateQueueRequest
                {
                    DispatchQueueUno = item.DispatchQueueUno,
                    DispatchStatusUno = 3,
                    VehicleUno = 0,
                    LockedBy = _workerOptions.InstanceId,
                    Error = null
                };

                var updated = await _apiClient.UpdateQueueAsync(inProgressUpdate, cancellationToken);
                if (!updated)
                {
                    _logger.LogWarning("Failed to mark dispatch_queue_uno {DispatchQueueUno} as in-progress (status 3).", item.DispatchQueueUno);
                }
                else
                {
                    await _mongoRepository.SetDispatchStatusAsync(item.DispatchQueueUno, 3, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert dispatch_queue_uno {DispatchQueueUno}", item.DispatchQueueUno);
            }
        }
    }
}
