using EpsonResetter.Core.Identification;

namespace EpsonResetter.Core.Compatibility;

/// <summary>
/// Registry of per-model capabilities. Lookup is by normalised model code so that
/// "ET-2850", "ET 2850 Series" and "EPSON ET2850" resolve to the same entry.
/// </summary>
public sealed class ModelCompatibilityRegistry
{
    private readonly Dictionary<string, ModelProfile> _byKey;

    public ModelCompatibilityRegistry(IEnumerable<ModelProfile> profiles)
    {
        Profiles = profiles.ToList();
        _byKey = new Dictionary<string, ModelProfile>(StringComparer.Ordinal);

        foreach (var profile in Profiles)
        {
            Index(profile.ModelKey, profile);
            Index(profile.DisplayName, profile);

            foreach (var alias in profile.Aliases)
            {
                Index(alias, profile);
            }
        }
    }

    public IReadOnlyList<ModelProfile> Profiles { get; }

    public ModelProfile? Find(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        foreach (var key in PrinterModelInfo.GetLookupKeys(modelName))
        {
            if (_byKey.TryGetValue(key, out var profile))
            {
                return profile;
            }
        }

        return null;
    }

    public bool IsKnown(string? modelName) => Find(modelName) is not null;

    private void Index(string value, ModelProfile profile)
    {
        foreach (var key in PrinterModelInfo.GetLookupKeys(value))
        {
            _byKey.TryAdd(key, profile);
        }
    }

    public static ModelCompatibilityRegistry CreateDefault() => new(BuildDefaultProfiles());

    private static IReadOnlyList<ModelProfile> BuildDefaultProfiles()
    {
        const string unsupportedReason =
            "No documented, vendor-authorised reset method has been verified for this model. " +
            "EPSON RESETTER does not distribute service utilities, activation keys or undocumented commands.";

        var models = new[]
        {
            "L3110", "L3150", "L3151", "L3160", "L3250", "L3251", "L3260", "L4150", "L4160",
            "L4260", "L6190", "L6290", "ET-2500", "ET-2550", "ET-2600", "ET-2650", "ET-2700",
            "ET-2750", "ET-2800", "ET-2850", "ET-3700", "ET-3750", "ET-4750", "XP-2100",
            "XP-2200", "XP-3100", "XP-4100", "XP-6100", "WF-2810", "WF-2850", "WF-7210",
            "WF-7710", "WF-7820"
        };

        return models
            .Select(model => new ModelProfile
            {
                ModelKey = PrinterModelInfo.Normalize(model),
                DisplayName = $"Epson {model}",
                Manufacturer = "Epson",
                CounterRead = SupportLevel.Unsupported,
                Reset = SupportLevel.Unsupported,
                Authorization = ResetAuthorization.None,
                Reason = unsupportedReason
            })
            .ToList();
    }
}