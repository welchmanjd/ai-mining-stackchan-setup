using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AiStackchanSetup.Steps;

public sealed class CompleteStep : StepBase
{
    public override int Index => 10;
    public override string Title => "完了";
    public override string Description => "セットアップが完了しました。必要ならログを作成してください。";
    public override string PrimaryActionText => "閉じる";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        Application.Current.Shutdown();
        return Task.FromResult(StepResult.Ok());
    }
}
