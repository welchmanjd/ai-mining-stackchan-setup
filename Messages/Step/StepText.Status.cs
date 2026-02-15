namespace AiStackchanSetup.Messages.Step;

internal static partial class StepText
{
    public const string Cancelled = "中止しました";
    public const string Timeout = "タイムアウトしました";

    public const string PortDetectionInProgress = "USBポートを検出しています..";
    public const string PortNotDetected = "未検出";
    public const string DeviceSettingsLoadInProgress = "デバイス設定を読み込み中...";
    public const string DeviceSettingsLoadCompleted = "デバイス設定の読み込みが完了しました。";
    public const string DeviceSettingsLoading = "読み込み中";
    public const string DeviceSettingsNotAcquired = "未取得";

    public const string FlashSkipped = "書き込みをスキップしました";
    public const string FlashSkipStatus = "ファームウェア書き込みをスキップ";
    public const string FlashInProgress = "書き込み中...";
    public const string FlashCompleted = "書き込み完了";
    public const string FlashStatusFailed = "書き込み失敗";

    public const string ApiPrecheckInProgress = "事前検証中...";
    public const string ApiPrecheckSuccess = "事前検証に成功";
    public const string ApiPrecheckSkippedUsingStoredKey = "M5StackCore2保存済みキーを再利用するため検証をスキップ";
    public const string OpenAiPrecheckNotRequired = "対象外 (Wi-Fi/AI OFF)";
    public const string AzurePrecheckNotRequired = "対象外 (Wi-Fi/機能 OFF)";
    public const string ConfigSendInProgress = "設定を送信中...";
    public const string ConfigReverifyInProgress = "設定を再確認中...";

    public const string PostRebootLogCaptureProgressFormat = "60秒ログを取得しています.. ({0}秒)";
    public const string ConfigSavedAndRebooted = "設定を保存して起動しました。";
    public const string ConfigSavedAndRebootedWithLogPathFormat = "設定を保存して起動しました。ログ保存: {0}";
    public const string ConfigSavedAndRebootedNoLog = "設定を保存して起動しました。(60秒のログ出力なし)";
    public const string ConfigSavedAndRebootedLogCaptureFailedFormat = "設定を保存して起動しました。(ログ取得失敗: {0})";
}
