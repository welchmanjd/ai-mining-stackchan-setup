namespace AiStackchanSetup.Messages.Step;

internal static partial class StepText
{
    public const string ComPortNotSelected = "接続先（COMポート）が未選択です";

    public const string OpenAiApiKeyRequired = "OpenAI APIキーが未入力です";
    public const string AzureKeyRequired = "Azureキーが未入力です";
    public const string AzureRegionRequired = "Azureリージョンが未入力です";
    public const string DuinoCoinUserRequired = "Duino-coinユーザー名が未入力です";
    public const string WifiSsidRequired = "wifi SSIDが未入力です";
    public const string WifiPasswordRequired = "wifiパスワードが未入力です";

    public const string FirmwareNotFoundError = "ファームウェア stackchan_core2_public.bin が見つかりません";
    public const string FirmwareNotFound = "ファームウェアが見つかりません";
    public const string FirmwarePublicBinOnly = "_public を含む .bin のみ対応しています";
    public const string FirmwareFormatInvalid = "ファームウェア形式が不正です";
    public const string FlashBaudNotNumeric = "通信速度（Baud）は数値で入力してください";
}
