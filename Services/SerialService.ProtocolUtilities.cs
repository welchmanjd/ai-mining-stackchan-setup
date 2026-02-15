using System;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

public partial class SerialService
{
    private static ConfigResult ParseOkResult(string response, string prefix)
    {
        return SerialProtocolLogic.ParseOkResult(response, prefix);
    }

    private static DeviceTestResult ParseTestResult(string response)
    {
        return SerialProtocolLogic.ParseTestResult(response);
    }

    private static string RedactCommand(string command)
    {
        return SerialProtocolLogic.RedactCommand(command);
    }

    // Kept for compatibility in this partial class.
    private static bool IsSensitiveKey(string key)
    {
        return key.Equals("wifi_pass", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("duco_miner_key", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("az_speech_key", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("openai_key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientInfoSyncNoise(string reason)
    {
        return SerialProtocolLogic.IsTransientInfoSyncNoise(reason);
    }
}
