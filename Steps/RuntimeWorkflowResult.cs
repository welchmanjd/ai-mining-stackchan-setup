namespace AiStackchanSetup.Steps;

internal readonly record struct RuntimeWorkflowResult(bool Succeeded, string Detail)
{
    public static RuntimeWorkflowResult Ok() => new(true, string.Empty);

    public static RuntimeWorkflowResult Fail(string detail) => new(false, detail);
}
