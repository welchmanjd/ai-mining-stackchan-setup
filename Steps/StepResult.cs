namespace AiStackchanSetup.Steps;

public enum StepStatus
{
    Success,
    Failed,
    Skipped,
    Cancelled
}

public sealed class StepResult
{
    public StepStatus Status { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    // Guidance is shown as "next action" text when a step fails.
    public string Guidance { get; init; } = string.Empty;
    public string Diagnostic { get; init; } = string.Empty;
    public bool CanRetry { get; init; }
    public bool CanSkip { get; init; }

    public static StepResult Ok() => new() { Status = StepStatus.Success };

    public static StepResult Skipped() => new() { Status = StepStatus.Skipped };

    public static StepResult Cancelled() => new() { Status = StepStatus.Cancelled };

    public static StepResult Fail(string errorMessage, string guidance = "", string diagnostic = "", bool canRetry = true, bool canSkip = false)
    {
        return new StepResult
        {
            Status = StepStatus.Failed,
            ErrorMessage = errorMessage,
            Guidance = guidance,
            Diagnostic = diagnostic,
            CanRetry = canRetry,
            CanSkip = canSkip
        };
    }
}
