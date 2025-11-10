using OpenAgent.Core.Tools;

namespace OpenAgent.Tools;

/// <summary>
/// Tool to get the current time and date.
/// </summary>
public class GetTimeTool : ITool
{
    public string Name => "get_current_time";

    public string Description => "Get the current date and time. Optionally specify a timezone (e.g., 'UTC', 'PST', 'EST').";

    public async Task<object> ExecuteAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Synchronous operation

        var timezone = arguments.TryGetValue("timezone", out var tzObj) && tzObj != null
            ? tzObj.ToString()
            : "Local";

        var now = timezone?.ToUpper() == "UTC"
            ? DateTime.UtcNow
            : DateTime.Now;

        return $"Current time ({timezone}): {now:yyyy-MM-dd HH:mm:ss}";
    }
}
