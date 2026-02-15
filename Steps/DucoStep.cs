using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class DucoStep : StepBase
{
    public DucoStep() : base(StepDefinitions.Duco, canRetry: false)
    {
    }

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (!vm.WifiEnabled)
        {
            return Task.FromResult(StepResult.Skipped());
        }

        if (!vm.MiningEnabled)
        {
            return Task.FromResult(StepResult.Ok());
        }

        if (string.IsNullOrWhiteSpace(vm.DucoUser))
        {
            return Task.FromResult(StepResult.Fail(StepMessages.DuinoCoinUserRequired, canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
