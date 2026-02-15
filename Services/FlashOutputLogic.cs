using System;

namespace AiStackchanSetup.Services;

internal static class FlashOutputLogic
{
    public static string ResolveEsptoolMessage(bool success, string stdout, string stderr)
    {
        if (success)
        {
            return "OK";
        }

        var combined = $"{stdout}\n{stderr}";
        if (combined.Contains("No serial data received.", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed to connect to ESP32: No serial data received.";
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return stderr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            return stdout.Trim();
        }

        return "esptool failed";
    }

    public static bool IsLikelyConnectFailure(string? message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("No serial data received", StringComparison.OrdinalIgnoreCase);
    }
}
