using System.Text.Json.Serialization;

namespace DispatcherService.Models;

public class GetQueueResponse
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public List<QueueItemDto>? Data { get; set; }
}
