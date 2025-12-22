using Microsoft.Extensions.Options;
using TelemetryDBBridgeService.Models;
using TelemetryDBBridgeService.Options;
using TelemetryDBBridgeService.Services;

namespace TelemetryDBBridgeService.HostedServices;

public class DispatcherHostedService : BackgroundService
{
    private readonly WorkerOptions _workerOptions;
    private readonly MongoRepository _mongoRepository;
    private readonly ApiClient _apiClient;
    private readonly ILogger<DispatcherHostedService> _logger;

    public DispatcherHostedService(
        IOptions<WorkerOptions> workerOptions,
        MongoRepository mongoRepository,
        ApiClient apiClient,
        ILogger<DispatcherHostedService> logger)
    {
        _workerOptions = workerOptions.Value;
        _mongoRepository = mongoRepository;
        _apiClient = apiClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_workerOptions.Enabled)
        {
            _logger.LogInformation("Worker disabled via configuration. Dispatcher will not start.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, _workerOptions.DispatchPollIntervalSeconds));
        _logger.LogInformation("Dispatcher starting with poll interval {Seconds}s", delay.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            DispatchQueueDocument? booking = null;

            try
            {
                _logger.LogInformation("Dispatcher polling for pending jobs...");
                booking = await _mongoRepository.ClaimNextPendingAsync(_workerOptions.InstanceId, stoppingToken);

                if (booking == null)
                {
                    _logger.LogInformation("No pending dispatch jobs found.");
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Dispatching queue item {DispatchQueueUno}", booking.DispatchQueueUno);
                await ProcessBookingAsync(booking, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogWarning("Dispatcher operation canceled; continuing loop.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected dispatcher error.");
                if (booking != null)
                {
                    await SafeResetAsync(booking.DispatchQueueUno, ex.Message, stoppingToken);
                }
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

        _logger.LogInformation("Dispatcher stopping.");
    }

    private async Task ProcessBookingAsync(DispatchQueueDocument booking, CancellationToken cancellationToken)
    {
        var vehicle = await _mongoRepository.FindNearestVehicleAsync(booking, cancellationToken);

        if (vehicle != null)
        {
            await HandleVehicleFoundAsync(booking, vehicle, cancellationToken);
            return;
        }

        await HandleNoVehicleAsync(booking, cancellationToken);
    }

    private async Task HandleVehicleFoundAsync(DispatchQueueDocument booking, VehicleLiveDocument vehicle, CancellationToken cancellationToken)
    {
        var updateRequest = new UpdateQueueRequest
        {
            DispatchQueueUno = booking.DispatchQueueUno,
            DispatchStatusUno = DispatchStatus.Done,
            VehicleUno = vehicle.VehicleUno,
            Error = null,
            LockedBy = _workerOptions.InstanceId,
            AttemptNumber = booking.AttemptCount ?? 0
        };

        var apiSuccess = await _apiClient.UpdateQueueAsync(updateRequest, cancellationToken);
        if (apiSuccess)
        {
            await _mongoRepository.MarkDoneAsync(booking.DispatchQueueUno, vehicle.VehicleUno, cancellationToken);
            _logger.LogInformation("Marked dispatch_queue_uno {DispatchQueueUno} as done with vehicle {VehicleUno}", booking.DispatchQueueUno, vehicle.VehicleUno);
        }
        else
        {
            await _mongoRepository.ResetPendingAsync(booking.DispatchQueueUno, "update_queue_failed", cancellationToken);
            _logger.LogWarning("UpdateQueue failed for dispatch_queue_uno {DispatchQueueUno}; reset to pending.", booking.DispatchQueueUno);
        }
    }

    private async Task HandleNoVehicleAsync(DispatchQueueDocument booking, CancellationToken cancellationToken)
    {
        var nextRetryCount = booking.RetryCount + 1;
        var hasReachedMaxRetry = nextRetryCount >= _workerOptions.MaxRetry;

        var updateRequest = new UpdateQueueRequest
        {
            DispatchQueueUno = booking.DispatchQueueUno,
            VehicleUno = 0,
            LockedBy = _workerOptions.InstanceId,
            AttemptNumber = booking.AttemptCount ?? nextRetryCount,
            DispatchStatusUno = hasReachedMaxRetry ? DispatchStatus.Failed : DispatchStatus.Pending,
            Error = hasReachedMaxRetry ? "no_vehicle_within_radius_final" : "no_vehicle_within_radius"
        };

        var apiSuccess = await _apiClient.UpdateQueueAsync(updateRequest, cancellationToken);

        if (!apiSuccess)
        {
            await _mongoRepository.ResetPendingAsync(booking.DispatchQueueUno, "update_queue_failed_no_vehicle", cancellationToken);
            _logger.LogWarning("UpdateQueue failed for no-vehicle path; dispatch_queue_uno {DispatchQueueUno} reset to pending.", booking.DispatchQueueUno);
            return;
        }

        if (hasReachedMaxRetry)
        {
            await _mongoRepository.MarkFailedAsync(booking.DispatchQueueUno, updateRequest.Error, nextRetryCount, cancellationToken);
            _logger.LogInformation("Dispatch_queue_uno {DispatchQueueUno} marked failed after {RetryCount} retries.", booking.DispatchQueueUno, nextRetryCount);
        }
        else
        {
            await _mongoRepository.ResetPendingAsync(booking.DispatchQueueUno, updateRequest.Error, cancellationToken);
            _logger.LogInformation("Dispatch_queue_uno {DispatchQueueUno} set to pending_dispatch for retry {RetryCount}.", booking.DispatchQueueUno, nextRetryCount);
        }
    }

    private async Task SafeResetAsync(long dispatchQueueUno, string? error, CancellationToken cancellationToken)
    {
        try
        {
            await _mongoRepository.ResetPendingAsync(dispatchQueueUno, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset dispatch_queue_uno {DispatchQueueUno}", dispatchQueueUno);
        }
    }
}
