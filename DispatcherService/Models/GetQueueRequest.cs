using System.Text.Json.Serialization;

namespace DispatcherService.Models;

public class GetQueueRequest
{
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("locked_by")]
    public string LockedBy { get; set; } = string.Empty;
}
