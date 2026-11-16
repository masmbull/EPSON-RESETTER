namespace EpsonResetter.Core.Models;

public enum ActivityResult
{
    Success = 0,
    Failure = 1,
    Unavailable = 2,
    Skipped = 3
}

/// <summary>
/// One meaningful action performed by the application. Persisted in the local
/// history database and never contains credentials of any kind.
/// </summary>
public sealed class ActivityRecord
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string PrinterName { get; set; } = string.Empty;

    public string? ModelName { get; set; }

    public string Action { get; set; } = string.Empty;

    public ActivityResult Result { get; set; } = ActivityResult.Success;

    public string? Details { get; set; }

    public string? Error { get; set; }

    public string TimestampDisplay => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}