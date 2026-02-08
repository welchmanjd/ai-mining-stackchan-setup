namespace AiStackchanSetup.Models;

public class DeviceConfig
{
    public bool WifiEnabled { get; set; } = true;
    public bool MiningEnabled { get; set; } = true;
    public bool AiEnabled { get; set; } = true;
    public string WifiSsid { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public string DucoUser { get; set; } = string.Empty;
    public string DucoMinerKey { get; set; } = string.Empty;
    public string OpenAiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = string.Empty;
    public string OpenAiInstructions { get; set; } = string.Empty;
    public string AzureKey { get; set; } = string.Empty;
    public string AzureRegion { get; set; } = string.Empty;
    public string AzureCustomSubdomain { get; set; } = string.Empty;
    public int DisplaySleepSeconds { get; set; } = 60;
    public int SpeakerVolume { get; set; } = 160;
    public string ShareAcceptedText { get; set; } = string.Empty;
    public string AttentionText { get; set; } = string.Empty;
    public string HelloText { get; set; } = string.Empty;

    public MaskedDeviceConfig ToMasked()
    {
        return new MaskedDeviceConfig
        {
            WifiEnabled = WifiEnabled,
            MiningEnabled = MiningEnabled,
            AiEnabled = AiEnabled,
            WifiSsid = WifiSsid,
            WifiPassword = Mask(WifiPassword),
            DucoUser = DucoUser,
            DucoMinerKey = Mask(DucoMinerKey),
            OpenAiKey = Mask(OpenAiKey),
            OpenAiModel = OpenAiModel,
            OpenAiInstructions = OpenAiInstructions,
            AzureKey = Mask(AzureKey),
            AzureRegion = AzureRegion,
            AzureCustomSubdomain = AzureCustomSubdomain,
            DisplaySleepSeconds = DisplaySleepSeconds,
            SpeakerVolume = SpeakerVolume,
            ShareAcceptedText = ShareAcceptedText,
            AttentionText = AttentionText,
            HelloText = HelloText
        };
    }

    public static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}

public class MaskedDeviceConfig
{
    public bool WifiEnabled { get; set; }
    public bool MiningEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public string WifiSsid { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public string DucoUser { get; set; } = string.Empty;
    public string DucoMinerKey { get; set; } = string.Empty;
    public string OpenAiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = string.Empty;
    public string OpenAiInstructions { get; set; } = string.Empty;
    public string AzureKey { get; set; } = string.Empty;
    public string AzureRegion { get; set; } = string.Empty;
    public string AzureCustomSubdomain { get; set; } = string.Empty;
    public int DisplaySleepSeconds { get; set; }
    public int SpeakerVolume { get; set; }
    public string ShareAcceptedText { get; set; } = string.Empty;
    public string AttentionText { get; set; } = string.Empty;
    public string HelloText { get; set; } = string.Empty;
}
