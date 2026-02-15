using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AiStackchanSetup.Steps;

public sealed class CompleteStep : StepBase
{
    public CompleteStep() : base(StepDefinitions.Complete, canRetry: false)
    {
    }

    public override Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        Application.Current.Shutdown();
        return Task.FromResult(StepResult.Ok());
    }
}
