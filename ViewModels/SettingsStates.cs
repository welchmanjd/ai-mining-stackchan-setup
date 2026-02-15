namespace AiStackchanSetup.ViewModels;

internal sealed class FlashSettingsState
{
    public string FirmwarePath = string.Empty;
    public string FlashBaudText = "921600";
    public bool FlashErase;
    public int FlashMode;
    public string FlashStatus = string.Empty;
    public string FirmwareInfoText = string.Empty;
    public string CurrentFirmwareInfoText = "未取得";
    public string FirmwareCompareMessage = string.Empty;
}

internal sealed class WifiSettingsState
{
    public string ConfigWifiSsid = string.Empty;
    public string ConfigWifiPassword = string.Empty;
    public bool WifiPasswordStored;
    public bool ReuseWifiPassword;
    public bool WifiEnabled = true;
    public bool MiningEnabled = true;
    public bool AiEnabled = true;
    public string DucoUser = string.Empty;
    public string DucoMinerKey = string.Empty;
    public bool DucoKeyStored;
    public bool ReuseDucoMinerKey;
}

internal sealed class AiSettingsState
{
    public string ConfigOpenAiKey = string.Empty;
    public bool OpenAiKeyStored;
    public bool ReuseOpenAiKey;
    public string ConfigOpenAiModel = "gpt-5-nano";
    public string ConfigOpenAiInstructions = "あなたはスタックチャンの会話AIです。日本語で短く答えてください。返答は120文字以内。箇条書き禁止。1〜2文。相手が『聞こえる？』等の確認なら、明るく短く返してください。";
    public string DisplaySleepSecondsText = "60";
    public bool CaptureSerialLogAfterReboot;
    public string SpeakerVolumeText = "100";
    public int SpeakerVolumeRaw = 100;
    public string ShareAcceptedText = "シェア獲得したよ！";
    public string AttentionText = "Hi";
    public string HelloText = "こんにちはマイニングスタックチャンです";
    public string AzureKey = string.Empty;
    public bool AzureKeyStored;
    public bool ReuseAzureKey;
    public string AzureRegion = string.Empty;
    public string AzureCustomSubdomain = string.Empty;
}
