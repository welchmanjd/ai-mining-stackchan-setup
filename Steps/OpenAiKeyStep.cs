using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class OpenAiKeyStep : StepBase
{
    public OpenAiKeyStep() : base(StepDefinitions.OpenAiKey, canRetry: false)
    {
    }

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (!vm.WifiEnabled || !vm.AiEnabled)
        {
            return Task.FromResult(StepResult.Skipped());
        }

        var hasKey = !string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey);
        var canReuseKey = vm.OpenAiKeyStored && vm.ReuseOpenAiKey;
        if (!hasKey && !canReuseKey)
        {
            return Task.FromResult(StepResult.Fail(StepMessages.OpenAiApiKeyRequired, canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
