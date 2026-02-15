using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup;

namespace AiStackchanSetup.Steps;

public sealed class AdditionalSettingsStep : StepBase
{
    public AdditionalSettingsStep() : base(StepDefinitions.AdditionalSettings, canRetry: false)
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

                if (!string.IsNullOrWhiteSpace(vm.DisplaySleepSecondsText) &&
                    (!int.TryParse(vm.DisplaySleepSecondsText, out var sleepSeconds) || sleepSeconds < 0))
                {
                    return Task.FromResult(StepResult.Fail(UiText.DisplaySleepSecondsInvalid, canRetry: false));
                }

                if (!string.IsNullOrWhiteSpace(vm.SpeakerVolumeText) &&
                    (!int.TryParse(vm.SpeakerVolumeText, out var speakerVolume) || speakerVolume < 0 || speakerVolume > 255))
                {
                    return Task.FromResult(StepResult.Fail(UiText.SpeakerVolumeInvalid, canRetry: false));
                }

                return Task.FromResult(StepResult.Ok());
            });
    }
}

