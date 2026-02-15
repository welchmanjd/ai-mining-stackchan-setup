namespace AiStackchanSetup.Steps;

public readonly record struct StepDefinition(
    int Index,
    string Title,
    string Description,
    string PrimaryActionText);
