using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelemetryDBBridgeService.Models;

[BsonIgnoreExtraElements]
public class DispatchQueueDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("dispatch_queue_uno")]
    public long DispatchQueueUno { get; set; }

    [BsonElement("service_booking_uno")]
    public long ServiceBookingUno { get; set; }

    [BsonElement("company_uno")]
    public long CompanyUno { get; set; }

    [BsonElement("pickup_time")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset PickupTime { get; set; }

    [BsonElement("pickup_latitude")]
    public double PickupLatitude { get; set; }

    [BsonElement("pickup_longitude")]
    public double PickupLongitude { get; set; }

    [BsonElement("radius_km")]
    public double RadiusKm { get; set; }

    [BsonElement("pickup_window_minutes")]
    public int PickupWindowMinutes { get; set; }

    [BsonElement("locked_by")]
    public string? LockedBy { get; set; }

    [BsonElement("locked_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? LockedOn { get; set; }

    [BsonElement("attempt_count")]
    public int? AttemptCount { get; set; }

    [BsonElement("created_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? CreatedOn { get; set; }

    [BsonElement("last_error")]
    public string? LastError { get; set; }

    [BsonElement("state")]
    public string State { get; set; } = "pending_dispatch";

    [BsonElement("retry_count")]
    public int RetryCount { get; set; }

    [BsonElement("updated_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset UpdatedOn { get; set; }

    [BsonElement("dispatch_status_uno")]
    public int DispatchStatusUno { get; set; }

    [BsonElement("processing_by")]
    public string? ProcessingBy { get; set; }

    [BsonElement("processing_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? ProcessingOn { get; set; }

    [BsonElement("vehicle_uno")]
    public long? VehicleUno { get; set; }

    [BsonElement("completed_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? CompletedOn { get; set; }
}
