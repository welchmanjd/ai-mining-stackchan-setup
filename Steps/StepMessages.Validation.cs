namespace AiStackchanSetup.Steps;

internal static partial class StepMessages
{
    public const string ComPortNotSelected = "COMポートが選択されていません";

    public const string OpenAiApiKeyRequired = "OpenAI APIキーが未入力です";
    public const string AzureKeyRequired = "Azureキーが未入力です";
    public const string AzureRegionRequired = "Azureリージョンが未入力です";
    public const string DuinoCoinUserRequired = "Duino-coinユーザー名が未入力です";
    public const string WifiSsidRequired = "Wi-Fi SSIDが未入力です";
    public const string WifiPasswordRequired = "Wi-Fiパスワードが未入力です";

    public const string FirmwareNotFoundError = "ファームウェア stackchan_core2_public.bin が見つかりません";
    public const string FirmwareNotFound = "ファームウェアが見つかりません";
    public const string FirmwarePublicBinOnly = "_public を含む .bin のみ対応しています";
    public const string FirmwareFormatInvalid = "ファームウェア形式が不正です";
    public const string FlashBaudNotNumeric = "Baud が数値ではありません";
}
