using EpsonResetter.Core.Models;

namespace EpsonResetter.Core.Compatibility;

public enum SupportLevel
{
    Unknown = 0,
    Unsupported = 1,
    Supported = 2
}

public enum ResetAuthorization
{
    None = 0,
    VendorDocumented = 1
}

/// <summary>
/// Compatibility information for one printer model. An entry only claims support
/// when a documented, vendor-authorised method has actually been verified for the
/// exact model; otherwise the levels stay <see cref="SupportLevel.Unsupported"/>.
/// </summary>
public sealed class ModelProfile
{
    public required string ModelKey { get; init; }

    public required string DisplayName { get; init; }

    public string Manufacturer { get; init; } = "Epson";

    public SupportLevel CounterRead { get; init; } = SupportLevel.Unsupported;

    public SupportLevel Reset { get; init; } = SupportLevel.Unsupported;

    public ResetAuthorization Authorization { get; init; } = ResetAuthorization.None;

    public string? VerifiedMethod { get; init; }

    public string? VerifiedMethodVersion { get; init; }

    public string? Reason { get; init; }

    public DateTime? VerifiedAtUtc { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    public bool SupportsCounterRead => CounterRead == SupportLevel.Supported;

    public bool SupportsReset => Reset == SupportLevel.Supported && Authorization == ResetAuthorization.VendorDocumented;

    public string SupportSummary => SupportsReset
        ? $"Reset available via {VerifiedMethod ?? "verified method"}."
        : Reason ?? "No supported reset method is recorded for this model.";
}