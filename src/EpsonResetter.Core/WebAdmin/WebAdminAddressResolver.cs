using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Core.WebAdmin;

/// <summary>
/// Turns spooler/port information into validated Web Admin candidates. It never
/// fabricates an address: when Windows exposes no IP, the caller is told to enter
/// one manually.
/// </summary>
public sealed partial class WebAdminAddressResolver
{
    private readonly AppSettings _settings;

    public WebAdminAddressResolver(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public WebAdminResolution Resolve(PrinterDevice printer, string? manualAddress = null)
    {
        ArgumentNullException.ThrowIfNull(printer);

        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(manualAddress))
        {
            var manual = WebAdminUrlValidator.Validate(manualAddress);
            if (!manual.IsValid)
            {
                warnings.Add(manual.Error ?? "The manually entered address is not valid.");
                return new WebAdminResolution { AddressSource = "manual", Warnings = warnings };
            }

            return new WebAdminResolution
            {
                DetectedAddress = manual.Host,
                AddressSource = "manual",
                Candidates = new[] { CreateCandidate(manual, manual.UsedInsecureScheme ? WebAdminScheme.Http : WebAdminScheme.Https, "manual") },
                Warnings = warnings
            };
        }

        var address = TryGetAddress(printer, out var source);
        if (address is null)
        {
            warnings.Add(
                printer.IsNetwork
                    ? "Windows reported a network printer but no IP address. Enter the address manually."
                    : "This printer is not connected over the network, so no Web Admin address can be detected. " +
                      "If it has a network interface, enter its IP address or hostname manually.");
            return new WebAdminResolution { AddressSource = "none", Warnings = warnings };
        }

        var candidates = new List<WebAdminCandidate>();
        foreach (var scheme in OrderSchemes())
        {
            var port = scheme == WebAdminScheme.Http ? _settings.HttpPort : _settings.HttpsPort;
            var url = $"{scheme.ToString().ToLowerInvariant()}://{address}{(port == DefaultPort(scheme) ? string.Empty : ":" + port)}/";
            var validation = WebAdminUrlValidator.Validate(url);
            if (!validation.IsValid)
            {
                warnings.Add(validation.Error ?? $"Could not build the {scheme} address.");
                continue;
            }

            var candidate = new WebAdminCandidate
            {
                Url = validation.NormalizedUrl!,
                Scheme = scheme,
                Host = address,
                Port = port,
                Source = source
            };

            if (!candidates.Any(existing => string.Equals(existing.Url, candidate.Url, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count > 0 && candidates[0].Scheme == WebAdminScheme.Http)
        {
            warnings.Add("The printer administration page will be opened over unencrypted HTTP.");
        }

        return new WebAdminResolution
        {
            DetectedAddress = address,
            AddressSource = source,
            Candidates = candidates,
            Warnings = warnings
        };
    }

    private IEnumerable<WebAdminScheme> OrderSchemes()
    {
        switch (_settings.PreferredScheme)
        {
            case WebAdminSchemePreference.Http:
                yield return WebAdminScheme.Http;
                yield return WebAdminScheme.Https;
                break;
            case WebAdminSchemePreference.Https:
                yield return WebAdminScheme.Https;
                yield return WebAdminScheme.Http;
                break;
            default:
                yield return WebAdminScheme.Http;
                yield return WebAdminScheme.Https;
                break;
        }
    }

    private static WebAdminCandidate CreateCandidate(WebAdminValidationResult validation, WebAdminScheme scheme, string source) =>
        new()
        {
            Url = validation.NormalizedUrl!,
            Scheme = scheme,
            Host = validation.Host!,
            Port = validation.Port ?? DefaultPort(scheme),
            Source = source
        };

    private static int DefaultPort(WebAdminScheme scheme) => scheme == WebAdminScheme.Http ? 80 : 443;

    private static string? TryGetAddress(PrinterDevice printer, out string source)
    {
        if (!string.IsNullOrWhiteSpace(printer.IpAddress))
        {
            source = "port";
            return printer.IpAddress.Trim();
        }

        var fromPort = ExtractAddress(printer.PortName);
        if (fromPort is not null)
        {
            source = "port-name";
            return fromPort;
        }

        source = "none";
        return null;
    }

    [GeneratedRegex(@"(?<ip>(?:\d{1,3}\.){3}\d{1,3})")]
    private static partial Regex IPv4Pattern();

    public static string? ExtractAddress(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return null;
        }

        var match = IPv4Pattern().Match(portName);
        if (!match.Success)
        {
            return null;
        }

        var candidate = match.Groups["ip"].Value;
        return IPAddress.TryParse(candidate, out _) ? candidate : null;
    }
}