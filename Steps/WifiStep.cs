using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class WifiStep : StepBase
{
    public override int Index => 3;
    public override string Title => "Wi-Fi";
    public override string Description => "Wi-Fiを設定します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (string.IsNullOrWhiteSpace(vm.ConfigWifiSsid) || string.IsNullOrWhiteSpace(vm.ConfigWifiPassword))
        {
            vm.ErrorMessage = "Wi-Fi情報が未入力です";
            return Task.FromResult(StepResult.Fail("Wi-Fi情報が未入力です", guidance: "SSID とパスワードを入力してください。", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
