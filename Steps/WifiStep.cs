using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class WifiStep : StepBase
{
    public WifiStep() : base(StepDefinitions.Wifi, canRetry: false)
    {
    }

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        return ExecuteStepAsync(
            context,
            token,
            () =>
            {
                var vm = context.ViewModel;
                if (!vm.WifiEnabled)
                {
                    return Task.FromResult(StepResult.Skipped());
                }

                if (string.IsNullOrWhiteSpace(vm.ConfigWifiSsid))
                {
                    return Task.FromResult(StepResult.Fail(StepText.WifiSsidRequired, canRetry: false));
                }

                var hasPassword = !string.IsNullOrWhiteSpace(vm.ConfigWifiPassword);
                var canReusePassword = vm.WifiPasswordStored && vm.ReuseWifiPassword;
                if (!hasPassword && !canReusePassword)
                {
                    return Task.FromResult(StepResult.Fail(StepText.WifiPasswordRequired, canRetry: false));
                }

                return Task.FromResult(StepResult.Ok());
            });
    }
}

