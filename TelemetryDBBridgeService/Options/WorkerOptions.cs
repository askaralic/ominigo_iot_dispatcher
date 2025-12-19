namespace TelemetryDBBridgeService.Options;

public class WorkerOptions
{
    public bool Enabled { get; set; }
    public string InstanceId { get; set; } = "worker";
    public int PollIntervalSeconds { get; set; } = 10;
    public int DispatchPollIntervalSeconds { get; set; } = 3;
    public int BatchSize { get; set; } = 50;
    public int MaxRetry { get; set; } = 5;
}
