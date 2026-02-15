using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

public partial class SerialService
{
    public Task<ConfigResult> SendConfigAsync(string portName, DeviceConfig config)
    {
        return SendConfigAsync(portName, config, CancellationToken.None);
    }

    public async Task<ConfigResult> SendConfigAsync(string portName, DeviceConfig config, CancellationToken token)
    {
        var warnings = new List<string>();
        async Task<ConfigResult> SendSetWithCompatAsync(string key, string value, bool allowUnknownKey)
        {
            var result = await SendSetAsync(portName, key, value, token);
            if (result.Success)
            {
                return result;
            }

            if (allowUnknownKey && result.Message.Contains("unknown_key", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{key}:unsupported");
                return new ConfigResult { Success = true, Message = "SKIP" };
            }

            return result;
        }

        async Task<ConfigResult> SendSetAnyAsync(string[] keys, string value)
        {
            ConfigResult? last = null;
            foreach (var key in keys)
            {
                var result = await SendSetAsync(portName, key, value, token);
                if (result.Success)
                {
                    return result;
                }

                if (result.Message.Contains("unknown_key", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"{key}:unsupported");
                    last = result;
                    continue;
                }

                return result;
            }

            return new ConfigResult
            {
                Success = true,
                Message = last?.Message ?? "SKIP"
            };
        }

        {
            var result = await SendSetWithCompatAsync("wifi_enabled", config.WifiEnabled ? "1" : "0", allowUnknownKey: true);
            if (!result.Success) return result;
        }
        {
            var result = await SendSetAnyAsync(
                new[] { "mining_enabled", "duco_enabled", "mining_on" },
                config.MiningEnabled ? "1" : "0");
            if (!result.Success) return result;
        }
        {
            var result = await SendSetWithCompatAsync("ai_enabled", config.AiEnabled ? "1" : "0", allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("wifi_ssid", config.WifiSsid, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.WifiPassword))
        {
            var result = await SendSetWithCompatAsync("wifi_pass", config.WifiPassword, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        var ducoUserToSend = config.DucoUser;
        var ducoKeyToSend = config.DucoMinerKey;
        {
            var result = await SendSetWithCompatAsync("duco_user", ducoUserToSend, allowUnknownKey: false);
            if (!result.Success) return result;
        }
        {
            var result = await SendSetWithCompatAsync("duco_miner_key", ducoKeyToSend, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("az_speech_region", config.AzureRegion, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.AzureKey))
        {
            var result = await SendSetWithCompatAsync("az_speech_key", config.AzureKey, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("az_custom_subdomain", config.AzureCustomSubdomain, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.OpenAiKey))
        {
            var result = await SendSetWithCompatAsync("openai_key", config.OpenAiKey, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("openai_model", config.OpenAiModel, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("openai_instructions", config.OpenAiInstructions, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("display_sleep_s", config.DisplaySleepSeconds.ToString(CultureInfo.InvariantCulture), allowUnknownKey: true);
            if (!result.Success) return result;
        }
        {
            var result = await SendSetWithCompatAsync("spk_volume", config.SpeakerVolume.ToString(CultureInfo.InvariantCulture), allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("share_accepted_text", config.ShareAcceptedText, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("attention_text", config.AttentionText, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("hello_text", config.HelloText, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        return new ConfigResult
        {
            Success = true,
            Message = warnings.Count > 0 ? $"一部キー未対応: {string.Join(", ", warnings)}" : "OK"
        };
    }

    public Task<ConfigResult> ApplyConfigAsync(string portName)
    {
        return ApplyConfigAsync(portName, CancellationToken.None);
    }

    public async Task<ConfigResult> ApplyConfigAsync(string portName, CancellationToken token)
    {
        try
        {
            var saveResponse = await SendCommandAsync(portName, "SAVE", TimeSpan.FromSeconds(10), token);
            if (!saveResponse.StartsWith("@OK SAVE", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigResult { Success = false, Message = "保存結果が不明です" };
            }

            try
            {
                var rebootResponse = await SendCommandAsync(portName, "REBOOT", TimeSpan.FromSeconds(10), token);
                if (!rebootResponse.StartsWith("@OK REBOOT", StringComparison.OrdinalIgnoreCase))
                {
                    return new ConfigResult { Success = false, Message = "再起動結果が不明です" };
                }
            }
            catch (TimeoutException)
            {
                return new ConfigResult { Success = true, Message = "OK (rebooting)" };
            }
            catch (IOException)
            {
                return new ConfigResult { Success = true, Message = "OK (rebooting)" };
            }

            return new ConfigResult { Success = true, Message = "OK" };
        }
        catch (SerialCommandException ex)
        {
            return new ConfigResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<ConfigResult> SendSetAsync(string portName, string key, string value, CancellationToken token)
    {
        try
        {
            var encodedValue = string.IsNullOrEmpty(value) ? EmptyValueSentinel : value;
            var response = await SendCommandAsync(portName, $"SET {key} {encodedValue}", TimeSpan.FromSeconds(8), token);
            if (response.StartsWith("@OK SET", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigResult { Success = true, Message = "OK" };
            }

            return new ConfigResult { Success = false, Message = $"設定結果が不明です ({key})" };
        }
        catch (SerialCommandException ex)
        {
            return new ConfigResult { Success = false, Message = $"設定失敗 ({key}): {ex.Reason}" };
        }
        catch (TimeoutException ex)
        {
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }
}
