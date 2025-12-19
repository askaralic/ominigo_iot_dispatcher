namespace TelemetryDBBridgeService.Options;

public class ApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string GetQueueEndpoint { get; set; } = string.Empty;
    public string UpdateQueueEndpoint { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
}
