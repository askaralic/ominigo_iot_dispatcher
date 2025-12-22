using System.Text.Json.Serialization;

namespace TelemetryDBBridgeService.Models;

public class UpdateQueueRequest
{
    [JsonPropertyName("dispatch_queue_uno")]
    public long DispatchQueueUno { get; set; }

    [JsonPropertyName("dispatch_status_uno")]
    public int DispatchStatusUno { get; set; }

    [JsonPropertyName("vehicle_uno")]
    public long VehicleUno { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("locked_by")]
    public string LockedBy { get; set; } = string.Empty;

     [JsonPropertyName("attempt_number")]
    public long AttemptNumber { get; set; }
}
