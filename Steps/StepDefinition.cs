using System;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

public readonly record struct StepDefinition(
    int Index,
    string Title,
    string Description,
    string PrimaryActionText,
    Func<MainViewModel, bool>? CanEnter = null)
{
    public bool IsAvailable(MainViewModel viewModel) => CanEnter?.Invoke(viewModel) ?? true;
}
