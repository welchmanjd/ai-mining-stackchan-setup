using System;
using System.Collections.Generic;
using System.Linq;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Infrastructure;

public static class SensitiveDataRedactor
{
    public static string Redact(string input, DeviceConfig config)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var secrets = new List<(string Value, string Masked)>
        {
            (config.WifiPassword, DeviceConfig.Mask(config.WifiPassword)),
            (config.DucoMinerKey, DeviceConfig.Mask(config.DucoMinerKey)),
            (config.OpenAiKey, DeviceConfig.Mask(config.OpenAiKey)),
            (config.AzureKey, DeviceConfig.Mask(config.AzureKey))
        };

        var output = input;
        foreach (var (value, masked) in secrets.Where(s => !string.IsNullOrWhiteSpace(s.Value)))
        {
            output = output.Replace(value, masked, StringComparison.Ordinal);
        }

        return output;
    }
}
