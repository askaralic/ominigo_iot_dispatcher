using System.Text.Json.Serialization;

namespace DispatcherService.Models;

public class UpdateQueueResponse
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("status_condition_uno")]
    public int? StatusConditionUno { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
