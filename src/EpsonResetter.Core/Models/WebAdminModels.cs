namespace EpsonResetter.Core.Models;

public enum WebAdminScheme
{
    Http = 0,
    Https = 1
}

public sealed class WebAdminCandidate
{
    public required string Url { get; init; }

    public WebAdminScheme Scheme { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; }

    public string Source { get; init; } = "detected";

    public override string ToString() => Url;
}

public sealed class WebAdminResolution
{
    public string? DetectedAddress { get; init; }

    public string AddressSource { get; init; } = "none";

    public IReadOnlyList<WebAdminCandidate> Candidates { get; init; } = Array.Empty<WebAdminCandidate>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public bool HasCandidates => Candidates.Count > 0;

    public string? PrimaryUrl => Candidates.Count > 0 ? Candidates[0].Url : null;
}

public sealed class WebAdminValidationResult
{
    public bool IsValid { get; init; }

    public string? NormalizedUrl { get; init; }

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? Error { get; init; }

    public bool UsedInsecureScheme { get; init; }

    public bool HasCredentials { get; init; }

    public static WebAdminValidationResult Invalid(string error) => new() { IsValid = false, Error = error };

    public static WebAdminValidationResult Valid(string normalizedUrl, string host, int port, bool insecure, bool hasCredentials) =>
        new()
        {
            IsValid = true,
            NormalizedUrl = normalizedUrl,
            Host = host,
            Port = port,
            UsedInsecureScheme = insecure,
            HasCredentials = hasCredentials
        };
}