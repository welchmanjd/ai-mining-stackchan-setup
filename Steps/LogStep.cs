using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class LogStep : StepBase
{
    public override int Index => 7;
    public override string Title => "ログ";
    public override string Description => "必要ならデバイスログを取得します。";
    public override string PrimaryActionText => "完了へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        return Task.FromResult(StepResult.Ok());
    }
}
