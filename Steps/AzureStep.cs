using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class AzureStep : StepBase
{
    public override int Index => 5;
    public override string Title => "Azure";
    public override string Description => "Azure Speechの設定を行います。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        return Task.FromResult(StepResult.Skipped());
    }
}
