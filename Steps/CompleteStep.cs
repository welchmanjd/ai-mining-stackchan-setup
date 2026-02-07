using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AiStackchanSetup.Steps;

public sealed class CompleteStep : StepBase
{
    public override int Index => 8;
    public override string Title => "完了";
    public override string Description => "セットアップ完了です。";
    public override string PrimaryActionText => "閉じる";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        Application.Current.Shutdown();
        return Task.FromResult(StepResult.Ok());
    }
}
