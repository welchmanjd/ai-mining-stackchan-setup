using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public abstract class StepBase : IStep
{
    public abstract int Index { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract string PrimaryActionText { get; }
    public virtual bool CanRetry => true;
    public virtual bool CanSkip => false;

    public virtual void OnEnter(StepContext context)
    {
    }

    public virtual void OnLeave(StepContext context)
    {
    }

    public abstract Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token);
}
