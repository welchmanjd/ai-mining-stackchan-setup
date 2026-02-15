using System;
using System.Collections.Generic;
using System.Linq;

namespace AiStackchanSetup.Steps;

internal static class StepCatalog
{
    private static readonly IReadOnlyDictionary<int, Func<IStep>> Factories = new Dictionary<int, Func<IStep>>
    {
        [StepDefinitions.DetectPorts.Index] = static () => new DetectPortsStep(),
        [StepDefinitions.Flash.Index] = static () => new FlashStep(),
        [StepDefinitions.FeatureToggle.Index] = static () => new FeatureToggleStep(),
        [StepDefinitions.Wifi.Index] = static () => new WifiStep(),
        [StepDefinitions.Duco.Index] = static () => new DucoStep(),
        [StepDefinitions.Azure.Index] = static () => new AzureStep(),
        [StepDefinitions.OpenAiKey.Index] = static () => new OpenAiKeyStep(),
        [StepDefinitions.AdditionalSettings.Index] = static () => new AdditionalSettingsStep(),
        [StepDefinitions.RuntimeSettings.Index] = static () => new RuntimeSettingsStep(),
        [StepDefinitions.Complete.Index] = static () => new CompleteStep()
    };

    public static IReadOnlyList<IStep> CreateDefaultSteps()
    {
        return StepDefinitions.All
            .Select(static definition => Create(definition.Index))
            .ToList();
    }

    private static IStep Create(int index)
    {
        if (!Factories.TryGetValue(index, out var factory))
        {
            throw new InvalidOperationException($"Step factory not found: {index}");
        }

        return factory();
    }
}
