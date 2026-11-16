namespace EpsonResetter.Core.Models;

public enum DiagnosticStatus
{
    Pass = 0,
    Warning = 1,
    Fail = 2,
    Skipped = 3
}

public sealed class DiagnosticCheck
{
    public required string Name { get; init; }

    public DiagnosticStatus Status { get; init; }

    public required string Message { get; init; }

    public string? Detail { get; init; }

    public static DiagnosticCheck Pass(string name, string message, string? detail = null) =>
        new() { Name = name, Status = DiagnosticStatus.Pass, Message = message, Detail = detail };

    public static DiagnosticCheck Warning(string name, string message, string? detail = null) =>
        new() { Name = name, Status = DiagnosticStatus.Warning, Message = message, Detail = detail };

    public static DiagnosticCheck Fail(string name, string message, string? detail = null) =>
        new() { Name = name, Status = DiagnosticStatus.Fail, Message = message, Detail = detail };

    public static DiagnosticCheck Skipped(string name, string message, string? detail = null) =>
        new() { Name = name, Status = DiagnosticStatus.Skipped, Message = message, Detail = detail };
}

public sealed class DiagnosticReport
{
    public required string PrinterName { get; init; }

    public string? ModelName { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public IReadOnlyList<DiagnosticCheck> Checks { get; init; } = Array.Empty<DiagnosticCheck>();

    public bool HasFailures => Checks.Any(check => check.Status == DiagnosticStatus.Fail);

    public bool HasWarnings => Checks.Any(check => check.Status == DiagnosticStatus.Warning);

    public int PassedCount => Checks.Count(check => check.Status == DiagnosticStatus.Pass);

    public string Summary => HasFailures
        ? $"{PassedCount}/{Checks.Count} checks passed - {Checks.Count(check => check.Status == DiagnosticStatus.Fail)} failed"
        : HasWarnings
            ? $"{PassedCount}/{Checks.Count} checks passed - {Checks.Count(check => check.Status == DiagnosticStatus.Warning)} warning(s)"
            : $"{PassedCount}/{Checks.Count} checks passed";
}