using System.Text.Json.Serialization;

namespace TelemetryDBBridgeService.Models;

public class UpdateQueueResponse
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
