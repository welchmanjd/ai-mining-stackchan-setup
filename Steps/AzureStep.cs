using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class AzureStep : StepBase
{
    public override int Index => 6;
    public override string Title => "Azure";
    public override string Description => "Azure Speechを設定します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        var needAzure = vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled);
        if (!needAzure)
        {
            return Task.FromResult(StepResult.Skipped());
        }

        if (string.IsNullOrWhiteSpace(vm.AzureRegion))
        {
            return Task.FromResult(StepResult.Fail("Azure Regionが未入力です", canRetry: false));
        }

        if (string.IsNullOrWhiteSpace(vm.AzureKey) && !vm.AzureKeyStored)
        {
            return Task.FromResult(StepResult.Fail("Azure Speechキーが未入力です", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
