using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelemetryDBBridgeService.Json;

namespace TelemetryDBBridgeService.Models;

[BsonIgnoreExtraElements]
public class QueueItemDto
{
    [JsonPropertyName("dispatch_queue_uno")]
    [BsonElement("dispatch_queue_uno")]
    public long DispatchQueueUno { get; set; }

    [JsonPropertyName("service_booking_uno")]
    [BsonElement("service_booking_uno")]
    public long ServiceBookingUno { get; set; }

    [JsonPropertyName("company_uno")]
    [BsonElement("company_uno")]
    public long CompanyUno { get; set; }

    [JsonPropertyName("pickup_time")]
    [BsonElement("pickup_time")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset PickupTime { get; set; }

    [JsonPropertyName("pickup_latitude")]
    [BsonElement("pickup_latitude")]
    public double PickupLatitude { get; set; }

    [JsonPropertyName("pickup_longitude")]
    [BsonElement("pickup_longitude")]
    public double PickupLongitude { get; set; }

    [JsonPropertyName("radius_km")]
    [BsonElement("radius_km")]
    public double RadiusKm { get; set; }

    [JsonPropertyName("pickup_window_minutes")]
    [BsonElement("pickup_window_minutes")]
    public int PickupWindowMinutes { get; set; }

    [JsonPropertyName("locked_by")]
    [BsonElement("locked_by")]
    public string? LockedBy { get; set; }

    [JsonPropertyName("locked_on")]
    [BsonElement("locked_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? LockedOn { get; set; }

    [JsonPropertyName("attempt_count")]
    [BsonElement("attempt_count")]
    public int? AttemptCount { get; set; }

    [JsonPropertyName("created_on")]
    [BsonElement("created_on")]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? CreatedOn { get; set; }

    [JsonPropertyName("last_error")]
    [BsonElement("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("dispatch_status_uno")]
    [BsonElement("dispatch_status_uno")]
    public int? DispatchStatusUno { get; set; }

    [JsonPropertyName("rejected_candidates_json")]
    [JsonConverter(typeof(RejectedCandidatesConverter))]
    [BsonElement("rejected_candidates_json")]
    public List<long>? RejectedCandidates { get; set; }
}
