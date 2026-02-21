namespace AiStackchanSetup.Messages.Ui;

internal static class UiText
{
    public const string Cancelled = "中止しました";
    public const string Retry = "もう一度ためす";
    public const string RetryTestGuidance = "テストに失敗しました。設定を見直して、もう一度ためしてください。";
    public const string FailureActionsPrompt = "うまくいかないときは、次の操作をためしてください。";
    public const string GuidanceFallback = "もう一度ためすか、サポート用ログを作成してください。";
    public const string Exit = "終了";

    public const string FlashWrite = "書き込み";
    public const string FlashSkipWrite = "書き込みをスキップ";

    public const string RunningTests = "テスト中...";
    public const string TestCompleted = "テスト完了";
    public const string TestFailed = "テストに失敗しました";
    public const string NotImplementedPossible = "未実装の可能性";

    public const string Available = "利用可能です";
    public const string AvailableChecked = "利用可能です (確認済み)";
    public const string NotEntered = "未入力";
    public const string NotExecuted = "未実行";
    public const string SuccessResultCode = "success";
    public const string FailResultCode = "fail";
    public const string SkippedResultCode = "skipped";
    public const string AzureKeyLabel = "Azureキー";
    public const string OpenAiKeyLabel = "OpenAIキー";

    public const string AzureKeyChecking = "Azureキーを確認中...";
    public const string AzureKeyCheckFailed = "Azureキー確認に失敗しました";
    public const string ApiKeyChecking = "APIキーを確認中...";
    public const string ApiKeyCheckCompleted = "APIキー確認が完了しました";
    public const string ApiKeyCheckFailed = "APIキー確認に失敗しました";
    public const string OpenAiKeyChecking = "OpenAIキーを確認中...";
    public const string OpenAiKeyCheckFailed = "OpenAIキー確認に失敗しました";
    public const string ApiValidationSkippedUsingDeviceInfo = "M5StackCore2内の情報を利用するため、APIキー確認をスキップしました。";

    public const string DeviceLogFetching = "デバイスログを取得中...";
    public const string DeviceLogEmpty = "デバイスログが空でした";
    public const string DeviceLogUnsupported = "デバイスがLOG_DUMPに対応していません";
    public const string DeviceLogFetchFailed = "デバイスログ取得に失敗しました";

    public const string SupportPackCreationFailed = "サポート用ログ作成に失敗しました";
    public const string SupportPackCreatedExitHint = "サポート用ログを作成しました。必要なら終了してください。";
    public const string BinFileDialogFilter = "BINファイル (*.bin)|*.bin|すべてのファイル (*.*)|*.*";

    public const string DisplaySleepSecondsInvalid = "画面スリープ秒数は0以上の数値で入力してください";
    public const string SpeakerVolumeInvalid = "スピーカー音量は0-255の数値で入力してください";

    public const string PortDetectedWithFirmwareInfoFormat = "{0} を検出しました（ファームウェア情報あり）";
    public const string PortDetectedWithBootLogInfoFormat = "{0} を検出しました（起動ログの情報あり）";
    public const string PortDetectedFormat = "{0} を検出しました";

    public static string Unavailable(string reason) => $"利用できません: {reason}";

    public static string KeyStatus(string keyName, bool success, string reason)
    {
        return success ? $"{keyName}: 利用可能です" : $"{keyName}: 利用できません ({reason})";
    }

    public static string DeviceLogSaved(string path) => $"デバイスログを保存しました: {path}";
    public static string SupportPackCreated(string path) => $"サポート用ログを作成: {path}";
}
