using System;
using System.Linq;
using System.Text.Json;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

internal static class SerialProtocolLogic
{
    public static ConfigResult ParseOkResult(string response, string prefix)
    {
        var json = response[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConfigResult { Success = false, Message = "結果JSONが空です" };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
            {
                return new ConfigResult { Success = true, Message = "OK" };
            }

            var reason = doc.RootElement.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() ?? "unknown"
                : "unknown";
            return new ConfigResult { Success = false, Message = reason };
        }
        catch
        {
            return new ConfigResult { Success = false, Message = "結果JSONを解析できません" };
        }
    }

    public static DeviceTestResult ParseTestResult(string response)
    {
        var json = response["TEST_RESULT".Length..].Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
            {
                return new DeviceTestResult { Success = true, Message = "OK" };
            }

            var stage = doc.RootElement.TryGetProperty("stage", out var stageProp)
                ? stageProp.GetString() ?? "unknown"
                : "unknown";
            var reason = doc.RootElement.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() ?? "unknown"
                : "unknown";
            return new DeviceTestResult { Success = false, Message = $"{stage}:{reason}" };
        }
        catch
        {
            return new DeviceTestResult { Success = false, Message = "結果JSONを解析できません" };
        }
    }

    public static string RedactCommand(string command)
    {
        if (!command.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
        {
            return command;
        }

        var parts = command.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return command;
        }

        var key = parts[1];
        if (IsSensitiveKey(key))
        {
            return $"{parts[0]} {parts[1]} {DeviceConfig.Mask(parts[2])}";
        }

        return command;
    }

    public static bool IsTransientInfoSyncNoise(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        if (!reason.StartsWith("unknown_cmd:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = reason["unknown_cmd:".Length..].Trim();
        return payload.Length > 0 && payload.All(ch => ch == 'U');
    }

    public static bool IsProtocolResponseLine(string line)
    {
        return line.StartsWith("@OK", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("@INFO", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("@CFG", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("@ERR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Equals("wifi_pass", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("duco_miner_key", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("az_speech_key", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("openai_key", StringComparison.OrdinalIgnoreCase);
    }
}
