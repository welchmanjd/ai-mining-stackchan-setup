using System;

namespace AiStackchanSetup.Infrastructure;

internal static class InputSanitizer
{
    public static string NormalizeWifiPassword(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Keep leading/trailing spaces as-is; only remove accidental line breaks.
        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    public static string NormalizeSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    public static bool HasEdgeWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]);
    }
}
