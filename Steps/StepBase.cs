using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public abstract class StepBase : IStep
{
    private readonly StepDefinition _definition;

    protected StepBase(StepDefinition definition, bool canRetry = true, bool canSkip = false)
    {
        _definition = definition;
        CanRetry = canRetry;
        CanSkip = canSkip;
    }

    public int Index => _definition.Index;
    public string Title => _definition.Title;
    public string Description => _definition.Description;
    public string PrimaryActionText => _definition.PrimaryActionText;
    public bool CanRetry { get; }
    public bool CanSkip { get; }

    public virtual void OnEnter(StepContext context)
    {
    }

    public virtual void OnLeave(StepContext context)
    {
    }

    public abstract Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token);
}
