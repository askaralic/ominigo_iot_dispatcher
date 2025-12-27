namespace DispatcherService.Options;

public class MongoOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string QueueCollection { get; set; } = string.Empty;
    public string VehicleLiveCollection { get; set; } = string.Empty;
}
