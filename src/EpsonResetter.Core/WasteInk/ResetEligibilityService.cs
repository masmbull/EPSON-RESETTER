using EpsonResetter.Core.Compatibility;
using EpsonResetter.Core.Models;

namespace EpsonResetter.Core.WasteInk;

public static class ResetEligibilityCodes
{
    public const string Eligible = "ELIGIBLE";
    public const string ModelUnknown = "MODEL_UNKNOWN";
    public const string ResetUnsupported = "RESET_UNSUPPORTED";
    public const string MethodNotAuthorised = "METHOD_NOT_AUTHORISED";
    public const string MethodNotConfirmed = "METHOD_NOT_CONFIRMED";
    public const string PrinterOffline = "PRINTER_OFFLINE";
    public const string PadNotInspected = "PAD_NOT_INSPECTED";
    public const string OwnerNotAuthorised = "OWNER_NOT_AUTHORISED";
}

public sealed record ResetEligibilityRequest
{
    public required string ModelName { get; init; }

    public bool PrinterOnline { get; init; }

    /// <summary>True only after the physical waste ink pad/collection system was inspected and serviced.</summary>
    public bool PadInspectedAndServiced { get; init; }

    public bool OwnerAuthorised { get; init; }

    public bool MethodConfirmedByOperator { get; init; }
}

public sealed class ResetEligibilityResult
{
    public bool IsEligible { get; init; }

    public required string Code { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    public ModelProfile? Profile { get; init; }

    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Pure policy engine for reset eligibility. It never performs a reset and never
/// assumes a method exists; it only reports whether every precondition for a
/// verified, authorised reset is satisfied.
/// </summary>
public sealed class ResetEligibilityService
{
    private readonly ModelCompatibilityRegistry _registry;

    public ResetEligibilityService(ModelCompatibilityRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ResetEligibilityResult Evaluate(ResetEligibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = _registry.Find(request.ModelName);
        var reasons = new List<string>();

        if (profile is null)
        {
            reasons.Add(
                $"Model '{request.ModelName}' is not in the compatibility registry. " +
                "Without a verified model profile a reset cannot be authorised.");
        }
        else
        {
            if (profile.Reset != SupportLevel.Supported)
            {
                reasons.Add($"Waste ink reset is not supported for {profile.DisplayName}.");
            }

            if (profile.Authorization != ResetAuthorization.VendorDocumented)
            {
                reasons.Add($"No vendor-authorised procedure has been verified for {profile.DisplayName}.");
            }
        }

        if (!request.MethodConfirmedByOperator)
        {
            reasons.Add("The operator has not confirmed the documented procedure for this exact model.");
        }

        if (!request.PrinterOnline)
        {
            reasons.Add("The printer is not reachable, so a reset could not be confirmed.");
        }

        if (!request.PadInspectedAndServiced)
        {
            reasons.Add("The waste ink pad or collection system must be inspected and serviced before a counter reset.");
        }

        if (!request.OwnerAuthorised)
        {
            reasons.Add("The equipment owner must authorise the reset.");
        }

        var eligible = reasons.Count == 0;
        var code = eligible ? ResetEligibilityCodes.Eligible : BuildCode(profile, request);

        return new ResetEligibilityResult
        {
            IsEligible = eligible,
            Code = code,
            Reasons = reasons,
            Profile = profile,
            Summary = eligible
                ? $"Reset preconditions are met for {profile?.DisplayName ?? request.ModelName}."
                : reasons[0]
        };
    }

    private static string BuildCode(ModelProfile? profile, ResetEligibilityRequest request)
    {
        if (profile is null)
        {
            return ResetEligibilityCodes.ModelUnknown;
        }

        if (profile.Reset != SupportLevel.Supported)
        {
            return ResetEligibilityCodes.ResetUnsupported;
        }

        if (profile.Authorization != ResetAuthorization.VendorDocumented)
        {
            return ResetEligibilityCodes.MethodNotAuthorised;
        }

        if (!request.MethodConfirmedByOperator)
        {
            return ResetEligibilityCodes.MethodNotConfirmed;
        }

        if (!request.PrinterOnline)
        {
            return ResetEligibilityCodes.PrinterOffline;
        }

        if (!request.PadInspectedAndServiced)
        {
            return ResetEligibilityCodes.PadNotInspected;
        }

        if (!request.OwnerAuthorised)
        {
            return ResetEligibilityCodes.OwnerNotAuthorised;
        }

        return ResetEligibilityCodes.Eligible;
    }
}