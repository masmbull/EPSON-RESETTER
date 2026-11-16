using EpsonResetter.Core.Compatibility;
using EpsonResetter.Core.Models;

namespace EpsonResetter.Core.WasteInk;

/// <summary>
/// Result of a waste ink counter read attempt. A value is only present when a
/// verified, permitted method produced it.
/// </summary>
public sealed class WasteInkReading
{
    public bool IsSupported { get; init; }

    public int? CounterValue { get; init; }

    public string? Method { get; init; }

    public required string Message { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static WasteInkReading Unsupported(string message) => new() { IsSupported = false, Message = message };

    public static WasteInkReading Read(int value, string method) =>
        new() { IsSupported = true, CounterValue = value, Method = method, Message = $"Counter read: {value} ({method})." };
}

public interface IWasteInkCounterReader
{
    WasteInkReading Read(PrinterDevice printer, ModelProfile? profile);
}

/// <summary>
/// Reads the counter only when the compatibility registry records a verified
/// method for the exact model. This build ships no device protocol module, so
/// every model resolves to an explicit "not supported" result instead of a
/// fabricated number.
/// </summary>
public sealed class RegistryBackedWasteInkCounterReader : IWasteInkCounterReader
{
    public WasteInkReading Read(PrinterDevice printer, ModelProfile? profile)
    {
        if (profile is null)
        {
            return WasteInkReading.Unsupported(
                $"The model '{printer.ModelName}' is not in the compatibility registry. No counter value can be read safely.");
        }

        if (!profile.SupportsCounterRead || string.IsNullOrWhiteSpace(profile.VerifiedMethod))
        {
            return WasteInkReading.Unsupported(
                $"Counter reading is not supported for {profile.DisplayName}. " +
                (profile.Reason ?? "No verified method is recorded."));
        }

        return WasteInkReading.Unsupported(
            $"The registry marks {profile.DisplayName} as readable with {profile.VerifiedMethod}, " +
            "but no verified reader module is installed in this build. No value is displayed.");
    }
}