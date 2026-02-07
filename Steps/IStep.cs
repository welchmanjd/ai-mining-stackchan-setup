using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public interface IStep
{
    int Index { get; }
    string Title { get; }
    string Description { get; }
    string PrimaryActionText { get; }
    bool CanRetry { get; }
    bool CanSkip { get; }

    void OnEnter(StepContext context);
    void OnLeave(StepContext context);
    Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token);
}
