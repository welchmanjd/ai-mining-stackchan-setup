using System;
using System.Collections.Generic;

namespace AiStackchanSetup.Steps;

internal static partial class StepDefinitions
{
    // Central ordered list so step flow can be composed from metadata instead of hard-coded arrays.
    public static readonly IReadOnlyList<StepDefinition> All =
    [
        DetectPorts,
        Flash,
        FeatureToggle,
        Wifi,
        Duco,
        Azure,
        OpenAiKey,
        AdditionalSettings,
        RuntimeSettings,
        Complete
    ];

    private static readonly IReadOnlyDictionary<int, StepDefinition> ByIndex = BuildIndexMap();

    public static StepDefinition GetByIndex(int index)
    {
        if (!ByIndex.TryGetValue(index, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Step definition not found: {index}");
        }

        return definition;
    }

    private static IReadOnlyDictionary<int, StepDefinition> BuildIndexMap()
    {
        var map = new Dictionary<int, StepDefinition>(All.Count);
        foreach (var definition in All)
        {
            if (!map.TryAdd(definition.Index, definition))
            {
                throw new InvalidOperationException($"Duplicate step index detected: {definition.Index}");
            }
        }

        return map;
    }
}
