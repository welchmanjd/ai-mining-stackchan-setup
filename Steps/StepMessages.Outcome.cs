namespace AiStackchanSetup.Steps;

internal static partial class StepMessages
{
    public const string UsbConnectionAndPortGuidance = "USB接続とCOMポート選択を確認してください。";
    public const string UsbConnectionAndDriverGuidance = "USB接続とドライバを確認してください。";
    public const string RetryByCheckingFlashLog = "書き込みログを確認してください。";

    public const string PortDetectionFailed = "ポート検出に失敗しました";
    public const string PortNotFound = "ポートが見つかりません";
    public const string PortNotFoundHelp = "ポートが見つかりません。USBケーブルとドライバを確認してください。";
    public const string FirmwareInfoNotAvailableHelp = "ファームウェア情報を取得できませんでした。接続先ポートを確認してください。";
    public const string SettingsLoadFailed = "設定読み込みに失敗しました";
    public const string FlashWriteFailed = "書き込みに失敗しました";

    public const string ApiPrecheckFailedPrefix = "事前確認に失敗";
    public const string ApiKeyValidationFailed = "APIキー確認に失敗しました";
    public const string ApiKeyValidationRetryGuidance = "APIキー確認に失敗しました。設定を見直して再試行してください。";

    public const string ConfigSendFailed = "設定送信に失敗しました";
    public const string ConfigSaveFailed = "設定保存に失敗しました";
    public const string ConfigVerificationFailed = "設定反映確認に失敗しました";
    public const string ConfigApplyFailed = "設定適用に失敗しました";
}
