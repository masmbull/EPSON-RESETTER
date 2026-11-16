using System.Text;
using System.Text.RegularExpressions;

namespace EpsonResetter.Core.Identification;

/// <summary>
/// Derives manufacturer and model information from the printer and driver names
/// that Windows reports. The result is always traceable to the spooler data.
/// </summary>
public static partial class PrinterModelInfo
{
    private static readonly string[] KnownManufacturers =
    {
        "Epson", "HP", "Hewlett-Packard", "Canon", "Brother", "Xerox", "Lexmark",
        "Dell", "Ricoh", "Kyocera", "Samsung", "Panasonic", "Sharp", "Toshiba",
        "Microsoft", "Fuji", "OKI", "Lenovo"
    };

    public static string ResolveManufacturer(string? printerName, string? driverName)
    {
        var haystack = $"{printerName} {driverName}";

        foreach (var manufacturer in KnownManufacturers)
        {
            if (haystack.Contains(manufacturer, StringComparison.OrdinalIgnoreCase))
            {
                return manufacturer.Equals("Hewlett-Packard", StringComparison.OrdinalIgnoreCase)
                    ? "HP"
                    : manufacturer;
            }
        }

        return "Unknown";
    }

    public static string ResolveModel(string? printerName, string? driverName)
    {
        var source = FirstNonEmpty(printerName, driverName) ?? string.Empty;

        // Network printer names often look like "EPSON ET-2850 on 192.168.1.20".
        var separator = source.IndexOf(" on ", StringComparison.OrdinalIgnoreCase);
        if (separator > 0)
        {
            source = source[..separator];
        }

        var cleaned = StripTrailingNoise(source);

        foreach (var manufacturer in KnownManufacturers)
        {
            if (cleaned.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[manufacturer.Length..];
                break;
            }
        }

        cleaned = StripTrailingNoise(cleaned);
        return cleaned.Length == 0 ? FirstNonEmpty(driverName, printerName)?.Trim() ?? string.Empty : cleaned;
    }

    /// <summary>
    /// Produces lookup keys for the compatibility registry, e.g.
    /// "EPSON L3250 Series" -> ["L3250SERIES", "L3250"].
    /// </summary>
    public static IReadOnlyList<string> GetLookupKeys(string? modelName)
    {
        var keys = new List<string>();
        var model = modelName ?? string.Empty;

        AddKey(keys, model);

        var withoutSeries = Regex.Replace(model, @"\b(series|printer|family|class)\b", " ", RegexOptions.IgnoreCase);
        AddKey(keys, withoutSeries);

        // Model codes are often written without the dash ("ET2850" vs "ET-2850").
        AddKey(keys, Regex.Replace(model, @"[^A-Za-z0-9]", string.Empty));

        return keys;
    }

    private static void AddKey(ICollection<string> keys, string value)
    {
        var normalized = Normalize(value);
        if (normalized.Length > 0 && !keys.Contains(normalized, StringComparer.Ordinal))
        {
            keys.Add(normalized);
        }
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string StripTrailingNoise(string value)
    {
        var trimmed = value.Trim();

        trimmed = Regex.Replace(trimmed, @"[\(\[][^\)\]]*[\)\]]\s*$", string.Empty);
        trimmed = Regex.Replace(trimmed, @"\s+(series|printer|family|class|device)$", string.Empty, RegexOptions.IgnoreCase);
        trimmed = trimmed.Trim(' ', '-', '_', ':');

        return trimmed;
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? null : second;
    }
}