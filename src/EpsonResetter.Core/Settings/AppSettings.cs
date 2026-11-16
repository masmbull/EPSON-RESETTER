namespace EpsonResetter.Core.Settings;

public enum WebAdminSchemePreference
{
    Auto = 0,
    Http = 1,
    Https = 2
}

public sealed class AppSettings
{
    public WebAdminSchemePreference PreferredScheme { get; set; } = WebAdminSchemePreference.Auto;

    public int HttpPort { get; set; } = 80;

    public int HttpsPort { get; set; } = 443;

    public int NetworkTimeoutMs { get; set; } = 1500;

    public bool ShowOnlyEpsonByDefault { get; set; } = true;

    public bool ConfirmBeforeOpeningAdmin { get; set; } = true;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (HttpPort is < 1 or > 65535)
        {
            errors.Add("HTTP port must be between 1 and 65535.");
        }

        if (HttpsPort is < 1 or > 65535)
        {
            errors.Add("HTTPS port must be between 1 and 65535.");
        }

        if (NetworkTimeoutMs is < 200 or > 60000)
        {
            errors.Add("Network timeout must be between 200 and 60000 milliseconds.");
        }

        return errors;
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}