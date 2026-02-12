using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class DucoStep : StepBase
{
    public override int Index => 5;
    public override string Title => "Duino-coin";
    public override string Description => "Duino-coin（マイニング機能）を設定します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

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
            return Task.FromResult(StepResult.Fail("Duino-coinユーザー名が未入力です", canRetry: false));
        }

        var hasKey = !string.IsNullOrWhiteSpace(vm.DucoMinerKey);
        var canReuseKey = vm.DucoKeyStored && vm.ReuseDucoMinerKey;
        if (!hasKey && !canReuseKey)
        {
            return Task.FromResult(StepResult.Fail("Duino-coinマイナーキーが未入力です", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
