using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class AdditionalSettingsStep : StepBase
{
    public override int Index => 8;
    public override string Title => "追加設定";
    public override string Description => "初期プロンプト以降の追加設定を入力します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;

        if (!string.IsNullOrWhiteSpace(vm.DisplaySleepSecondsText) &&
            (!int.TryParse(vm.DisplaySleepSecondsText, out var sleepSeconds) || sleepSeconds < 0))
        {
            return Task.FromResult(StepResult.Fail("画面スリープ秒数は0以上の整数で入力してください", canRetry: false));
        }

        if (!string.IsNullOrWhiteSpace(vm.SpeakerVolumeText) &&
            (!int.TryParse(vm.SpeakerVolumeText, out var speakerVolume) || speakerVolume < 0 || speakerVolume > 255))
        {
            return Task.FromResult(StepResult.Fail("スピーカー音量は0-255の整数で入力してください", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
