using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class WifiStep : StepBase
{
    public override int Index => 4;
    public override string Title => "Wi-Fi";
    public override string Description => "Wi-Fiを設定します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (!vm.WifiEnabled)
        {
            return Task.FromResult(StepResult.Skipped());
        }

        if (string.IsNullOrWhiteSpace(vm.ConfigWifiSsid))
        {
            return Task.FromResult(StepResult.Fail("Wi-Fi SSIDが未入力です", canRetry: false));
        }

        var hasPassword = !string.IsNullOrWhiteSpace(vm.ConfigWifiPassword);
        var canReusePassword = vm.WifiPasswordStored && vm.ReuseWifiPassword;
        if (!hasPassword && !canReusePassword)
        {
            return Task.FromResult(StepResult.Fail("Wi-Fiパスワードが未入力です", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
