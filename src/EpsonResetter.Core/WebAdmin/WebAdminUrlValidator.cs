using System.Net;
using System.Net.Sockets;
using EpsonResetter.Core.Models;

namespace EpsonResetter.Core.WebAdmin;

/// <summary>
/// Validates and normalises a user supplied Web Admin address.
/// Only http/https URLs are accepted, credentials are rejected outright and
/// TLS validation is never altered.
/// </summary>
public static class WebAdminUrlValidator
{
    private const int MaxHostLength = 253;

    public static WebAdminValidationResult Validate(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return WebAdminValidationResult.Invalid("Enter the printer IP address or hostname.");
        }

        var input = rawInput.Trim();

        // Accept bare hosts such as "192.168.1.50" or "printer.local:8080".
        if (!input.Contains("://", StringComparison.Ordinal))
        {
            input = "http://" + input;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return WebAdminValidationResult.Invalid("The address is not a valid URL.");
        }

        if (!uri.IsAbsoluteUri)
        {
            return WebAdminValidationResult.Invalid("The address must be an absolute URL.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return WebAdminValidationResult.Invalid("Only http:// and https:// addresses are supported.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return WebAdminValidationResult.Invalid(
                "URLs containing a username or password are not accepted. Enter only the printer address.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return WebAdminValidationResult.Invalid("The address is missing a host name.");
        }

        if (!IsSupportedHost(uri.Host))
        {
            return WebAdminValidationResult.Invalid(
                "The host must be a valid IP address or DNS name (letters, digits and hyphens only).");
        }

        if (uri.Port < 1 || uri.Port > 65535)
        {
            return WebAdminValidationResult.Invalid("The port number is outside the valid range (1-65535).");
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Path = string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/" ? "/" : uri.AbsolutePath
        };

        var normalized = builder.Uri.AbsoluteUri;

        return WebAdminValidationResult.Valid(
            normalized,
            uri.Host,
            uri.Port,
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase),
            hasCredentials: false);
    }

    private static bool IsSupportedHost(string host)
    {
        if (host.Length == 0 || host.Length > MaxHostLength)
        {
            return false;
        }

        // IPv6 literals arrive wrapped in brackets.
        var candidate = host;
        if (candidate.StartsWith('[') && candidate.EndsWith(']'))
        {
            candidate = candidate[1..^1];
        }

        if (IPAddress.TryParse(candidate, out _))
        {
            return true;
        }

        if (candidate.Contains(':') || candidate.Contains(' ') || candidate.Contains('/'))
        {
            return false;
        }

        foreach (var label in candidate.Split('.'))
        {
            if (label.Length == 0 || label.Length > 63)
            {
                return false;
            }

            if (label.StartsWith('-') || label.EndsWith('-'))
            {
                return false;
            }

            foreach (var character in label)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    return false;
                }
            }
        }

        return true;
    }
}