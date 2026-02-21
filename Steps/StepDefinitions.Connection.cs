namespace AiStackchanSetup.Steps;

internal static partial class StepDefinitions
{
    public static readonly StepDefinition DetectPorts = new(1, "接続", "USB接続先を探します。", "探す");
    public static readonly StepDefinition Flash = new(2, "書き込み", "ファームウェアを書き込みます。", "書き込み");
    public static readonly StepDefinition FeatureToggle = new(3, "機能", "wifi・マイニング・AI の有効/無効を設定します。", "次へ");
}
