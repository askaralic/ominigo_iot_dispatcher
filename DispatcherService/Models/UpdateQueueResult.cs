namespace DispatcherService.Models;

public class UpdateQueueResult
{
    public bool IsSuccess { get; init; }

    public int? StatusConditionUno { get; init; }

    public string? Message { get; init; }

    public bool ShouldMarkFailed => StatusConditionUno == 1;
}
