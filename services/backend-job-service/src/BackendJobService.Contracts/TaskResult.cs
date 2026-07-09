namespace BackendJobService.Contracts;

public class TaskResult
{
    public bool Success { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }

    public static TaskResult Ok(string? outputJson = null) => new() { Success = true, OutputJson = outputJson };

    public static TaskResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
