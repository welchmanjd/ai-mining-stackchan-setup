using System;
using System.Text.Json;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    public DeviceConfig BuildDeviceConfig()
    {
        var displaySleepSeconds = 60;
        if (!string.IsNullOrWhiteSpace(DisplaySleepSecondsText) &&
            int.TryParse(DisplaySleepSecondsText, out var parsedSleep) &&
            parsedSleep >= 0)
        {
            displaySleepSeconds = parsedSleep;
        }

        var speakerVolume = 160;
        if (!string.IsNullOrWhiteSpace(SpeakerVolumeText) &&
            int.TryParse(SpeakerVolumeText, out var parsedVolume) &&
            parsedVolume >= 0 && parsedVolume <= 255)
        {
            speakerVolume = parsedVolume;
        }

        var wifiEnabled = WifiEnabled;
        var miningEnabled = wifiEnabled && MiningEnabled;
        var aiEnabled = wifiEnabled && AiEnabled;

        var ducoUserToSend = DucoUser;
        var ducoKeyToSend = (ReuseDucoMinerKey && DucoKeyStored) ? "" : DucoMinerKey;

        return new DeviceConfig
        {
            WifiEnabled = wifiEnabled,
            MiningEnabled = miningEnabled,
            AiEnabled = aiEnabled,
            WifiSsid = ConfigWifiSsid,
            WifiPassword = (ReuseWifiPassword && WifiPasswordStored) ? "" : ConfigWifiPassword,
            DucoUser = ducoUserToSend,
            DucoMinerKey = ducoKeyToSend,
            OpenAiKey = (ReuseOpenAiKey && OpenAiKeyStored) ? "" : ConfigOpenAiKey,
            OpenAiModel = ConfigOpenAiModel,
            OpenAiInstructions = ConfigOpenAiInstructions,
            AzureKey = (ReuseAzureKey && AzureKeyStored) ? "" : AzureKey,
            AzureRegion = AzureRegion,
            AzureCustomSubdomain = AzureCustomSubdomain,
            DisplaySleepSeconds = displaySleepSeconds,
            SpeakerVolume = speakerVolume,
            ShareAcceptedText = ShareAcceptedText,
            AttentionText = AttentionText,
            HelloText = HelloText
        };
    }

    public void ApplyConfigSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (TryGetString(root, "wifi_ssid", out var ssid) && !string.IsNullOrWhiteSpace(ssid))
            {
                ConfigWifiSsid = ssid;
            }
            var hasWifiPassSet =
                TryGetBool(root, "wifi_pass_set", out var wifiPassSet) ||
                TryGetBool(root, "wifi_password_set", out wifiPassSet);
            if (hasWifiPassSet)
            {
                WifiPasswordStored = wifiPassSet;
                ReuseWifiPassword = wifiPassSet;
            }
            else if (TryGetString(root, "wifi_pass", out var wifiPassRaw) && !string.IsNullOrWhiteSpace(wifiPassRaw))
            {
                WifiPasswordStored = true;
                ReuseWifiPassword = true;
            }

            if (TryGetBool(root, "wifi_enabled", out var wifiEnabled))
            {
                WifiEnabled = wifiEnabled;
            }
            if (TryGetBool(root, "mining_enabled", out var miningEnabled))
            {
                MiningEnabled = miningEnabled;
            }
            if (TryGetBool(root, "ai_enabled", out var aiEnabled))
            {
                AiEnabled = aiEnabled;
            }

            if (TryGetString(root, "duco_user", out var ducoUser) && !string.IsNullOrWhiteSpace(ducoUser))
            {
                DucoUser = ducoUser;
            }
            var hasDucoKeySet =
                TryGetBool(root, "duco_key_set", out var ducoKeySet) ||
                TryGetBool(root, "duco_miner_key_set", out ducoKeySet);
            if (hasDucoKeySet)
            {
                DucoKeyStored = ducoKeySet;
                ReuseDucoMinerKey = ducoKeySet;
            }
            else if (TryGetString(root, "duco_miner_key", out var ducoKeyRaw) && !string.IsNullOrWhiteSpace(ducoKeyRaw))
            {
                DucoKeyStored = true;
                ReuseDucoMinerKey = true;
            }

            if ((TryGetString(root, "az_region", out var azRegion) ||
                 TryGetString(root, "az_speech_region", out azRegion)) &&
                !string.IsNullOrWhiteSpace(azRegion))
            {
                AzureRegion = azRegion;
            }
            if (TryGetString(root, "az_custom_subdomain", out var azSubdomain) && !string.IsNullOrWhiteSpace(azSubdomain))
            {
                AzureCustomSubdomain = azSubdomain;
            }
            else if (TryGetString(root, "az_endpoint", out var azEndpoint) && !string.IsNullOrWhiteSpace(azEndpoint))
            {
                AzureCustomSubdomain = azEndpoint;
            }
            var hasAzKeySet =
                TryGetBool(root, "az_key_set", out var azKeySet) ||
                TryGetBool(root, "az_speech_key_set", out azKeySet);
            if (hasAzKeySet)
            {
                AzureKeyStored = azKeySet;
                ReuseAzureKey = azKeySet;
            }
            else if (TryGetString(root, "az_speech_key", out var azKeyRaw) && !string.IsNullOrWhiteSpace(azKeyRaw))
            {
                AzureKeyStored = true;
                ReuseAzureKey = true;
            }

            if (TryGetString(root, "openai_model", out var openAiModel) && !string.IsNullOrWhiteSpace(openAiModel))
            {
                ConfigOpenAiModel = openAiModel;
            }
            if (TryGetString(root, "openai_instructions", out var openAiInstructions) && !string.IsNullOrWhiteSpace(openAiInstructions))
            {
                ConfigOpenAiInstructions = openAiInstructions;
            }
            var hasOpenAiKeySet =
                TryGetBool(root, "openai_key_set", out var openAiKeySet) ||
                TryGetBool(root, "openai_api_key_set", out openAiKeySet);
            if (hasOpenAiKeySet && openAiKeySet && string.IsNullOrWhiteSpace(ConfigOpenAiKey))
            {
                OpenAiKeyStored = true;
                ReuseOpenAiKey = true;
                MaskedOpenAiKey = "(保存済み)";
            }
            else if (hasOpenAiKeySet)
            {
                OpenAiKeyStored = openAiKeySet;
                ReuseOpenAiKey = openAiKeySet;
            }
            else if (TryGetString(root, "openai_key", out var openAiKeyRaw) && !string.IsNullOrWhiteSpace(openAiKeyRaw))
            {
                OpenAiKeyStored = true;
                ReuseOpenAiKey = true;
                MaskedOpenAiKey = "(保存済み)";
            }

            if (TryGetInt(root, "display_sleep_s", out var displaySleepSeconds))
            {
                DisplaySleepSecondsText = displaySleepSeconds.ToString();
            }
            if (TryGetInt(root, "spk_volume", out var speakerVolume))
            {
                SpeakerVolumeText = speakerVolume.ToString();
            }

            if (TryGetString(root, "share_accepted_text", out var shareAcceptedText) && !string.IsNullOrWhiteSpace(shareAcceptedText))
            {
                ShareAcceptedText = shareAcceptedText;
            }
            if (TryGetString(root, "attention_text", out var attentionText) && !string.IsNullOrWhiteSpace(attentionText))
            {
                AttentionText = attentionText;
            }
            if (TryGetString(root, "hello_text", out var helloText) && !string.IsNullOrWhiteSpace(helloText))
            {
                HelloText = helloText;
            }
        }
        catch
        {
            // best effort: keep current/default input values
        }
    }

    private string NormalizeOpenAiModel(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return _openAiModelOptions[0];
        }

        foreach (var option in _openAiModelOptions)
        {
            if (string.Equals(option, trimmed, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return _openAiModelOptions[0];
    }

    private (int Filled, int Missing) CalculateInputStatus()
    {
        var filled = 0;
        var missing = 0;

        void Count(bool ok)
        {
            if (ok) filled++;
            else missing++;
        }

        Count(SelectedPort != null);

        if (WifiEnabled)
        {
            Count(!string.IsNullOrWhiteSpace(ConfigWifiSsid));
            Count((ReuseWifiPassword && WifiPasswordStored) || !string.IsNullOrWhiteSpace(ConfigWifiPassword));

            if (MiningEnabled)
            {
                Count(!string.IsNullOrWhiteSpace(DucoUser));
            }

            if (MiningEnabled || AiEnabled)
            {
                Count(!string.IsNullOrWhiteSpace(AzureRegion));
                Count((ReuseAzureKey && AzureKeyStored) || !string.IsNullOrWhiteSpace(AzureKey));
            }

            if (AiEnabled)
            {
                Count((ReuseOpenAiKey && OpenAiKeyStored) || !string.IsNullOrWhiteSpace(ConfigOpenAiKey));
            }
        }

        return (filled, missing);
    }

    private static bool TryGetString(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(key, out var elem))
        {
            return false;
        }

        if (elem.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = elem.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetBool(JsonElement root, string key, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(key, out var elem))
        {
            return false;
        }

        if (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False)
        {
            value = elem.GetBoolean();
            return true;
        }

        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var n))
        {
            value = n != 0;
            return true;
        }

        return false;
    }

    private static bool TryGetInt(JsonElement root, string key, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(key, out var elem))
        {
            return false;
        }

        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var n))
        {
            value = n;
            return true;
        }

        return false;
    }
}
