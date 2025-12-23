using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using TelemetryDBBridgeService.Models;
using TelemetryDBBridgeService.Options;

namespace TelemetryDBBridgeService.Services;

public class MongoRepository
{
    private readonly IMongoCollection<DispatchQueueDocument> _queueCollection;
    private readonly IMongoCollection<VehicleLiveDocument> _vehicleCollection;
    private readonly ILogger<MongoRepository> _logger;
    private readonly Lazy<Task> _indexInitializer;

    public MongoRepository(IOptions<MongoOptions> mongoOptions, ILogger<MongoRepository> logger)
    {
        _logger = logger;
        var options = mongoOptions.Value;

        var client = new MongoClient(options.ConnectionString);
        var database = client.GetDatabase(options.Database);
        _queueCollection = database.GetCollection<DispatchQueueDocument>(options.QueueCollection);
        _vehicleCollection = database.GetCollection<VehicleLiveDocument>(options.VehicleLiveCollection);

        _indexInitializer = new Lazy<Task>(() => CreateIndexesAsync());
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _indexInitializer.Value.WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // If index creation fails due to permissions or existing indexes, continue without blocking the worker.
            _logger.LogWarning(ex, "Skipping index creation; continuing without indexes.");
        }
    }

    private async Task CreateIndexesAsync()
    {
        try
        {
            var queueIndexes = new List<CreateIndexModel<DispatchQueueDocument>>
            {
                new(Builders<DispatchQueueDocument>.IndexKeys.Ascending(x => x.DispatchQueueUno),
                    new CreateIndexOptions { Unique = true, Name = "ux_dispatch_queue_uno" }),
                new(Builders<DispatchQueueDocument>.IndexKeys.Ascending(x => x.State),
                    new CreateIndexOptions { Name = "ix_state" })
            };

            await _queueCollection.Indexes.CreateManyAsync(queueIndexes);

            var vehicleIndexes = new List<CreateIndexModel<VehicleLiveDocument>>
            {
                new(Builders<VehicleLiveDocument>.IndexKeys.Geo2DSphere(x => x.Loc),
                    new CreateIndexOptions { Name = "ix_loc_2dsphere" }),
                new(new BsonDocumentIndexKeysDefinition<VehicleLiveDocument>(
                        new BsonDocument { { "lon", 1 }, { "lat", 1 } }),
                    new CreateIndexOptions { Name = "ix_lon_lat" })
            };

            await _vehicleCollection.Indexes.CreateManyAsync(vehicleIndexes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MongoDB indexes");
            throw;
        }
    }

    public async Task<(int AttemptCount, bool IsNew)> UpsertQueueItemAsync(QueueItemDto queueItem, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var filter = Builders<DispatchQueueDocument>.Filter.Eq(x => x.DispatchQueueUno, queueItem.DispatchQueueUno);

        var existing = await _queueCollection
            .Find(filter)
            .Project(x => new { x.State, x.RetryCount, x.DispatchStatusUno, x.AttemptCount })
            .FirstOrDefaultAsync(cancellationToken);

        var attemptCount = existing?.AttemptCount ?? queueItem.AttemptCount ?? 0;
        var retryCount = existing?.RetryCount ?? queueItem.AttemptCount ?? 0;
        var existingStatus = existing?.DispatchStatusUno;
        if (existingStatus == 0) existingStatus = DispatchStatus.Pending; // normalize old data
        var dispatchStatus = queueItem.DispatchStatusUno ?? existingStatus ?? DispatchStatus.Pending;
        var preserveProcessing = string.Equals(existing?.State, "processing", StringComparison.OrdinalIgnoreCase);

        var updateBuilder = Builders<DispatchQueueDocument>.Update
            .Set(x => x.ServiceBookingUno, queueItem.ServiceBookingUno)
            .Set(x => x.CompanyUno, queueItem.CompanyUno)
            .Set(x => x.PickupTime, queueItem.PickupTime)
            .Set(x => x.PickupLatitude, queueItem.PickupLatitude)
            .Set(x => x.PickupLongitude, queueItem.PickupLongitude)
            .Set(x => x.RadiusKm, queueItem.RadiusKm)
            .Set(x => x.PickupWindowMinutes, queueItem.PickupWindowMinutes)
            .Set(x => x.LockedBy, queueItem.LockedBy)
            .Set(x => x.LockedOn, queueItem.LockedOn)
            .Set(x => x.AttemptCount, attemptCount)
            .Set(x => x.CreatedOn, queueItem.CreatedOn)
            .Set(x => x.LastError, queueItem.LastError)
            .Set(x => x.RetryCount, retryCount)
            .Set(x => x.DispatchStatusUno, dispatchStatus)
            .Set(x => x.UpdatedOn, now)
            .SetOnInsert(x => x.DispatchQueueUno, queueItem.DispatchQueueUno);

        if (preserveProcessing)
        {
            // Keep processing state/locks intact if already claimed by dispatcher.
            updateBuilder = updateBuilder
                .Set(x => x.State, existing!.State)
                .Set(x => x.ProcessingBy, null)
                .Set(x => x.ProcessingOn, null);
        }
        else
        {
            updateBuilder = updateBuilder
                .Set(x => x.State, "pending_dispatch")
                .Set(x => x.ProcessingBy, null)
                .Set(x => x.ProcessingOn, null)
                .Set(x => x.VehicleUno, null)
                .Set(x => x.CompletedOn, null);
        }

        var options = new UpdateOptions { IsUpsert = true };
        var result = await _queueCollection.UpdateOneAsync(filter, updateBuilder, options, cancellationToken);
        var isNew = result.UpsertedId != null;
        return (attemptCount, isNew);
    }

    public async Task<DispatchQueueDocument?> ClaimNextPendingAsync(string instanceId, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var filter = Builders<DispatchQueueDocument>.Filter.Eq(x => x.State, "pending_dispatch");
        var update = Builders<DispatchQueueDocument>.Update
            .Set(x => x.State, "processing")
            .Set(x => x.ProcessingBy, instanceId)
            .Set(x => x.ProcessingOn, now)
            .Inc(x => x.AttemptCount, 1)
            .Set(x => x.DispatchStatusUno, DispatchStatus.InProgress)
            .Set(x => x.UpdatedOn, now);

        var options = new FindOneAndUpdateOptions<DispatchQueueDocument>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<DispatchQueueDocument>.Sort.Ascending(x => x.UpdatedOn)
        };

        var result = await _queueCollection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        if (result != null)
        {
            _logger.LogDebug("Claimed dispatch_queue_uno {DispatchQueueUno} for processing by {InstanceId}", result.DispatchQueueUno, instanceId);
        }

        return result;
    }

    public async Task ResetPendingAsync(long dispatchQueueUno, string? lastError, CancellationToken cancellationToken, int? dispatchStatusUno = null)
    {
        await EnsureIndexesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var filter = Builders<DispatchQueueDocument>.Filter.Eq(x => x.DispatchQueueUno, dispatchQueueUno);
        var update = Builders<DispatchQueueDocument>.Update
            .Set(x => x.State, "pending_dispatch")
            .Set(x => x.LastError, lastError)
            .Inc(x => x.RetryCount, 1)
            .Set(x => x.DispatchStatusUno, dispatchStatusUno ?? DispatchStatus.Pending)
            .Set(x => x.ProcessingBy, null)
            .Set(x => x.ProcessingOn, null)
            .Set(x => x.UpdatedOn, now);

        await _queueCollection.UpdateOneAsync(filter, update, new UpdateOptions(), cancellationToken);
    }

    public async Task MarkDoneAsync(long dispatchQueueUno, long vehicleUno, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var filter = Builders<DispatchQueueDocument>.Filter.Eq(x => x.DispatchQueueUno, dispatchQueueUno);
        var update = Builders<DispatchQueueDocument>.Update
            .Set(x => x.State, "done")
            .Set(x => x.VehicleUno, vehicleUno)
            .Set(x => x.DispatchStatusUno, DispatchStatus.Done)
            .Set(x => x.CompletedOn, now)
            .Set(x => x.UpdatedOn, now);

        await _queueCollection.UpdateOneAsync(filter, update, new UpdateOptions(), cancellationToken);
    }

    public async Task MarkFailedAsync(long dispatchQueueUno, string? lastError, int? retryCount, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var filter = Builders<DispatchQueueDocument>.Filter.Eq(x => x.DispatchQueueUno, dispatchQueueUno);
        var update = Builders<DispatchQueueDocument>.Update
            .Set(x => x.State, "failed")
            .Set(x => x.LastError, lastError)
            .Set(x => x.DispatchStatusUno, DispatchStatus.Failed)
            .Set(x => x.CompletedOn, now)
            .Set(x => x.UpdatedOn, now)
            .Set(x => x.ProcessingBy, null)
            .Set(x => x.ProcessingOn, null);

        if (retryCount.HasValue)
        {
            update = update.Set(x => x.RetryCount, retryCount.Value);
        }

        await _queueCollection.UpdateOneAsync(filter, update, new UpdateOptions(), cancellationToken);
    }

    public async Task SetDispatchStatusAsync(long dispatchQueueUno, int dispatchStatusUno, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var filter = Builders<DispatchQueueDocument>.Filter.Eq(x => x.DispatchQueueUno, dispatchQueueUno);
        var update = Builders<DispatchQueueDocument>.Update
            .Set(x => x.DispatchStatusUno, dispatchStatusUno)
            .Set(x => x.UpdatedOn, DateTimeOffset.UtcNow);

        await _queueCollection.UpdateOneAsync(filter, update, new UpdateOptions(), cancellationToken);
    }

    public async Task<VehicleLiveDocument?> FindNearestVehicleAsync(DispatchQueueDocument booking, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var filters = new List<(FilterDefinition<VehicleLiveDocument> Filter, string Label)>
        {
            (Builders<VehicleLiveDocument>.Filter.And(
                Builders<VehicleLiveDocument>.Filter.Eq(x => x.CompanyUno, booking.CompanyUno),
                Builders<VehicleLiveDocument>.Filter.Eq(x => x.StatusUno, 1)), "company+status"),
            (Builders<VehicleLiveDocument>.Filter.Eq(x => x.CompanyUno, booking.CompanyUno), "company-only"),
            (FilterDefinition<VehicleLiveDocument>.Empty, "all-vehicles")
        };

        var point = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(
            booking.PickupLongitude,
            booking.PickupLatitude));

        foreach (var (filter, label) in filters)
        {
            var queryDocument = filter.Render(_vehicleCollection.DocumentSerializer, _vehicleCollection.Settings.SerializerRegistry);

            try
            {
                var geoNearStage = new BsonDocument("$geoNear", new BsonDocument
                {
                    { "near", new BsonDocument
                        {
                            { "type", "Point" },
                            { "coordinates", new BsonArray { point.Coordinates.Longitude, point.Coordinates.Latitude } }
                        }
                    },
                    { "distanceField", "distance_meters" },
                    { "maxDistance", booking.RadiusKm * 1000 },
                    { "spherical", true },
                    { "query", queryDocument }
                });

                var pipeline = new[] { geoNearStage, new BsonDocument("$limit", 1) };

                var vehicle = await _vehicleCollection.Aggregate<VehicleLiveDocument>(pipeline).FirstOrDefaultAsync(cancellationToken);
                if (vehicle != null)
                {
                    _logger.LogInformation("GeoNear found vehicle {VehicleUno} for dispatch_queue_uno {DispatchQueueUno} using filter {FilterLabel}", vehicle.VehicleUno, booking.DispatchQueueUno, label);
                    return vehicle;
                }

                _logger.LogInformation("GeoNear returned no vehicle for dispatch_queue_uno {DispatchQueueUno} with filter {FilterLabel}", booking.DispatchQueueUno, label);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GeoNear query failed with filter {FilterLabel}; falling back to in-memory distance calculation.", label);
                break;
            }
        }

        // Fallback: pull a limited set and compute distance in memory using lat/lon fields.
        foreach (var (filter, label) in filters)
        {
            var candidates = await _vehicleCollection.Find(filter)
                .Project(x => new VehicleLiveDocument
                {
                    VehicleUno = x.VehicleUno,
                    CompanyUno = x.CompanyUno,
                    StatusUno = x.StatusUno,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude
                })
                .Limit(500)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Fallback candidate count ({FilterLabel}) for dispatch_queue_uno {DispatchQueueUno}: {Count}", label, booking.DispatchQueueUno, candidates.Count);

            var withinRadius = candidates
                .Where(c => c.Latitude.HasValue && c.Longitude.HasValue)
                .Select(c => new
                {
                    Vehicle = c,
                    Distance = HaversineDistanceMeters(booking.PickupLatitude, booking.PickupLongitude, c.Latitude!.Value, c.Longitude!.Value)
                })
                .Where(x => x.Distance <= booking.RadiusKm * 1000)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (withinRadius != null)
            {
                withinRadius.Vehicle.DistanceMeters = withinRadius.Distance;
                _logger.LogInformation("Selected vehicle {VehicleUno} at distance {DistanceMeters}m for dispatch_queue_uno {DispatchQueueUno} using filter {FilterLabel}", withinRadius.Vehicle.VehicleUno, withinRadius.Vehicle.DistanceMeters, booking.DispatchQueueUno, label);
                return withinRadius.Vehicle;
            }
        }

        _logger.LogInformation("No vehicle found within {RadiusKm} km for dispatch_queue_uno {DispatchQueueUno}", booking.RadiusKm, booking.DispatchQueueUno);
        return null;
    }

    private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // meters
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180);
}
