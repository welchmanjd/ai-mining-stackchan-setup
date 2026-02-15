namespace AiStackchanSetup.Steps;

internal static partial class StepDefinitions
{
    public static readonly StepDefinition RuntimeSettings = new(9, "設定保存", "APIキー確認と設定保存を実行します。", "設定保存");
    public static readonly StepDefinition Complete = new(10, "完了", "セットアップが完了しました。", "閉じる");
}
