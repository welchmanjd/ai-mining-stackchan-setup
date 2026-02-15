using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class AdditionalSettingsStep : StepBase
{
    public AdditionalSettingsStep() : base(StepDefinitions.AdditionalSettings, canRetry: false)
    {
    }

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;

        if (!string.IsNullOrWhiteSpace(vm.DisplaySleepSecondsText) &&
            (!int.TryParse(vm.DisplaySleepSecondsText, out var sleepSeconds) || sleepSeconds < 0))
        {
            return Task.FromResult(StepResult.Fail("画面スリープ秒数は0以上の数値で入力してください", canRetry: false));
        }

        if (!string.IsNullOrWhiteSpace(vm.SpeakerVolumeText) &&
            (!int.TryParse(vm.SpeakerVolumeText, out var speakerVolume) || speakerVolume < 0 || speakerVolume > 255))
        {
            return Task.FromResult(StepResult.Fail("スピーカー音量は0-255の数値で入力してください", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
