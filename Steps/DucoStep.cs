using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class DucoStep : StepBase
{
    public override int Index => 4;
    public override string Title => "Duino-coin";
    public override string Description => "Duino-coin情報を入力します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        return Task.FromResult(StepResult.Ok());
    }
}
