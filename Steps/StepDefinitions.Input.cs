namespace AiStackchanSetup.Steps;

internal static partial class StepDefinitions
{
    public static readonly StepDefinition Wifi = new(4, "Wi-Fi", "Wi-Fiを設定します。", "次へ");
    public static readonly StepDefinition Duco = new(
        5,
        "Duino-coin",
        "Duino-coin（マイニング）設定を入力します。",
        "次へ",
        vm => vm.WifiEnabled);

    public static readonly StepDefinition Azure = new(
        6,
        "Azure",
        "Azure Speechを設定します。",
        "次へ",
        vm => vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled));

    public static readonly StepDefinition OpenAiKey = new(
        7,
        "OpenAIキー",
        "OpenAI APIキーを設定します。",
        "次へ",
        vm => vm.WifiEnabled && vm.AiEnabled);

    public static readonly StepDefinition AdditionalSettings = new(8, "追加設定", "追加設定を入力します。", "次へ");
}
