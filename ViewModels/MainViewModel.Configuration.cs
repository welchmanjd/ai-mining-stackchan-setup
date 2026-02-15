using System;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: map between UI inputs and device config/json snapshots.
    public DeviceConfig BuildDeviceConfig()
    {
        return _configurationStateService.BuildDeviceConfig(this);
    }

    public void ApplyConfigSnapshot(string json)
    {
        _configurationStateService.ApplyConfigSnapshot(this, json);
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
}
