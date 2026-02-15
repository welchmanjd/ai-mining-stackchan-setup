using System;
using System.Text.Json;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.ViewModels;

internal sealed class ConfigurationStateService
{
    public DeviceConfig BuildDeviceConfig(MainViewModel vm)
    {
        var displaySleepSeconds = 60;
        if (!string.IsNullOrWhiteSpace(vm.DisplaySleepSecondsText) &&
            int.TryParse(vm.DisplaySleepSecondsText, out var parsedSleep) &&
            parsedSleep >= 0)
        {
            displaySleepSeconds = parsedSleep;
        }

        var speakerVolume = 160;
        if (!string.IsNullOrWhiteSpace(vm.SpeakerVolumeText) &&
            int.TryParse(vm.SpeakerVolumeText, out var parsedVolume) &&
            parsedVolume >= 0 && parsedVolume <= 255)
        {
            speakerVolume = parsedVolume;
        }

        var wifiEnabled = vm.WifiEnabled;
        var miningEnabled = wifiEnabled && vm.MiningEnabled;
        var aiEnabled = wifiEnabled && vm.AiEnabled;

        var ducoUserToSend = vm.DucoUser;
        var ducoKeyToSend = (vm.ReuseDucoMinerKey && vm.DucoKeyStored) ? "" : vm.DucoMinerKey;

        return new DeviceConfig
        {
            WifiEnabled = wifiEnabled,
            MiningEnabled = miningEnabled,
            AiEnabled = aiEnabled,
            WifiSsid = vm.ConfigWifiSsid,
            WifiPassword = (vm.ReuseWifiPassword && vm.WifiPasswordStored) ? "" : vm.ConfigWifiPassword,
            DucoUser = ducoUserToSend,
            DucoMinerKey = ducoKeyToSend,
            OpenAiKey = (vm.ReuseOpenAiKey && vm.OpenAiKeyStored) ? "" : vm.ConfigOpenAiKey,
            OpenAiModel = vm.ConfigOpenAiModel,
            OpenAiInstructions = vm.ConfigOpenAiInstructions,
            AzureKey = (vm.ReuseAzureKey && vm.AzureKeyStored) ? "" : vm.AzureKey,
            AzureRegion = vm.AzureRegion,
            AzureCustomSubdomain = vm.AzureCustomSubdomain,
            DisplaySleepSeconds = displaySleepSeconds,
            SpeakerVolume = speakerVolume,
            ShareAcceptedText = vm.ShareAcceptedText,
            AttentionText = vm.AttentionText,
            HelloText = vm.HelloText
        };
    }

    public void ApplyConfigSnapshot(MainViewModel vm, string json)
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
                vm.ConfigWifiSsid = ssid;
            }
            var hasWifiPassSet =
                TryGetBool(root, "wifi_pass_set", out var wifiPassSet) ||
                TryGetBool(root, "wifi_password_set", out wifiPassSet);
            if (hasWifiPassSet)
            {
                vm.WifiPasswordStored = wifiPassSet;
                vm.ReuseWifiPassword = wifiPassSet;
            }
            else if (TryGetString(root, "wifi_pass", out var wifiPassRaw) && !string.IsNullOrWhiteSpace(wifiPassRaw))
            {
                vm.WifiPasswordStored = true;
                vm.ReuseWifiPassword = true;
            }

            if (TryGetBool(root, "wifi_enabled", out var wifiEnabled))
            {
                vm.WifiEnabled = wifiEnabled;
            }
            if (TryGetBool(root, "mining_enabled", out var miningEnabled))
            {
                vm.MiningEnabled = miningEnabled;
            }
            if (TryGetBool(root, "ai_enabled", out var aiEnabled))
            {
                vm.AiEnabled = aiEnabled;
            }

            if (TryGetString(root, "duco_user", out var ducoUser) && !string.IsNullOrWhiteSpace(ducoUser))
            {
                vm.DucoUser = ducoUser;
            }
            var hasDucoKeySet =
                TryGetBool(root, "duco_key_set", out var ducoKeySet) ||
                TryGetBool(root, "duco_miner_key_set", out ducoKeySet);
            if (hasDucoKeySet)
            {
                vm.DucoKeyStored = ducoKeySet;
                vm.ReuseDucoMinerKey = ducoKeySet;
            }
            else if (TryGetString(root, "duco_miner_key", out var ducoKeyRaw) && !string.IsNullOrWhiteSpace(ducoKeyRaw))
            {
                vm.DucoKeyStored = true;
                vm.ReuseDucoMinerKey = true;
            }

            if ((TryGetString(root, "az_region", out var azRegion) ||
                 TryGetString(root, "az_speech_region", out azRegion)) &&
                !string.IsNullOrWhiteSpace(azRegion))
            {
                vm.AzureRegion = azRegion;
            }
            if (TryGetString(root, "az_custom_subdomain", out var azSubdomain) && !string.IsNullOrWhiteSpace(azSubdomain))
            {
                vm.AzureCustomSubdomain = azSubdomain;
            }
            else if (TryGetString(root, "az_endpoint", out var azEndpoint) && !string.IsNullOrWhiteSpace(azEndpoint))
            {
                vm.AzureCustomSubdomain = azEndpoint;
            }
            var hasAzKeySet =
                TryGetBool(root, "az_key_set", out var azKeySet) ||
                TryGetBool(root, "az_speech_key_set", out azKeySet);
            if (hasAzKeySet)
            {
                vm.AzureKeyStored = azKeySet;
                vm.ReuseAzureKey = azKeySet;
            }
            else if (TryGetString(root, "az_speech_key", out var azKeyRaw) && !string.IsNullOrWhiteSpace(azKeyRaw))
            {
                vm.AzureKeyStored = true;
                vm.ReuseAzureKey = true;
            }

            if (TryGetString(root, "openai_model", out var openAiModel) && !string.IsNullOrWhiteSpace(openAiModel))
            {
                vm.ConfigOpenAiModel = openAiModel;
            }
            if (TryGetString(root, "openai_instructions", out var openAiInstructions) && !string.IsNullOrWhiteSpace(openAiInstructions))
            {
                vm.ConfigOpenAiInstructions = openAiInstructions;
            }
            var hasOpenAiKeySet =
                TryGetBool(root, "openai_key_set", out var openAiKeySet) ||
                TryGetBool(root, "openai_api_key_set", out openAiKeySet);
            if (hasOpenAiKeySet && openAiKeySet && string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey))
            {
                vm.OpenAiKeyStored = true;
                vm.ReuseOpenAiKey = true;
                vm.MaskedOpenAiKey = "(保存済み)";
            }
            else if (hasOpenAiKeySet)
            {
                vm.OpenAiKeyStored = openAiKeySet;
                vm.ReuseOpenAiKey = openAiKeySet;
            }
            else if (TryGetString(root, "openai_key", out var openAiKeyRaw) && !string.IsNullOrWhiteSpace(openAiKeyRaw))
            {
                vm.OpenAiKeyStored = true;
                vm.ReuseOpenAiKey = true;
                vm.MaskedOpenAiKey = "(保存済み)";
            }

            if (TryGetInt(root, "display_sleep_s", out var displaySleepSeconds))
            {
                vm.DisplaySleepSecondsText = displaySleepSeconds.ToString();
            }
            if (TryGetInt(root, "spk_volume", out var speakerVolume))
            {
                vm.SpeakerVolumeText = speakerVolume.ToString();
            }

            if (TryGetString(root, "share_accepted_text", out var shareAcceptedText) && !string.IsNullOrWhiteSpace(shareAcceptedText))
            {
                vm.ShareAcceptedText = shareAcceptedText;
            }
            if (TryGetString(root, "attention_text", out var attentionText) && !string.IsNullOrWhiteSpace(attentionText))
            {
                vm.AttentionText = attentionText;
            }
            if (TryGetString(root, "hello_text", out var helloText) && !string.IsNullOrWhiteSpace(helloText))
            {
                vm.HelloText = helloText;
            }
        }
        catch
        {
            // best effort: keep current/default input values
        }
    }

    public bool IsUsingStoredApiKeys(MainViewModel vm)
    {
        return (vm.OpenAiKeyStored && vm.ReuseOpenAiKey) || (vm.AzureKeyStored && vm.ReuseAzureKey);
    }

    public void RaiseApiValidationStateChanged(
        MainViewModel vm,
        Action<string> raisePropertyChanged,
        AsyncRelayCommand validateApiKeysCommand)
    {
        raisePropertyChanged(nameof(MainViewModel.CanRunApiValidation));
        raisePropertyChanged(nameof(MainViewModel.ApiValidationGuideText));
        validateApiKeysCommand.RaiseCanExecuteChanged();
    }

    public void RefreshApiValidationSummaries(
        MainViewModel vm,
        string reuseValidationSkippedText,
        Action<bool?> setApiSummaryVisual,
        Action<bool?> setAzureSummaryVisual)
    {
        if (vm.OpenAiKeyStored && vm.ReuseOpenAiKey)
        {
            vm.ApiTestSummary = reuseValidationSkippedText;
            setApiSummaryVisual(null);
        }
        else if (vm.ApiTestSummary == reuseValidationSkippedText)
        {
            vm.ApiTestSummary = UiText.NotExecuted;
            setApiSummaryVisual(null);
        }

        if (vm.AzureKeyStored && vm.ReuseAzureKey)
        {
            vm.AzureTestSummary = reuseValidationSkippedText;
            setAzureSummaryVisual(null);
        }
        else if (vm.AzureTestSummary == reuseValidationSkippedText)
        {
            vm.AzureTestSummary = UiText.NotExecuted;
            setAzureSummaryVisual(null);
        }
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
