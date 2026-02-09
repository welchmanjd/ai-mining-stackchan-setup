using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class OpenAiKeyStep : StepBase
{
    public override int Index => 7;
    public override string Title => "OpenAIキー";
    public override string Description => "OpenAI APIキーを確認します。";
    public override string PrimaryActionText => "次へ";
    public override bool CanRetry => false;

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (!vm.WifiEnabled || !vm.AiEnabled)
        {
            return Task.FromResult(StepResult.Skipped());
        }

        if (string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey) && !vm.OpenAiKeyStored)
        {
            return Task.FromResult(StepResult.Fail("OpenAI APIキーが未入力です", canRetry: false));
        }

        return Task.FromResult(StepResult.Ok());
    }
}
