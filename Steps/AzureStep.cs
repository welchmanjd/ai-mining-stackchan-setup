using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class AzureStep : StepBase
{
    public AzureStep() : base(StepDefinitions.Azure, canRetry: false)
    {
    }

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
            return Task.FromResult(StepResult.Fail(StepMessages.AzureRegionRequired, canRetry: false));
        }

        var hasKey = !string.IsNullOrWhiteSpace(vm.AzureKey);
        var canReuseKey = vm.AzureKeyStored && vm.ReuseAzureKey;
        if (!hasKey && !canReuseKey)
        {
            return Task.FromResult(StepResult.Fail(StepMessages.AzureKeyRequired, canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
