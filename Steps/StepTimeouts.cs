using System;

namespace AiStackchanSetup.Steps;

public sealed class StepTimeouts
{
    public TimeSpan PortDetect { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan Flash { get; init; } = TimeSpan.FromMinutes(3);
    public TimeSpan Hello { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan SendConfig { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan ApplyConfig { get; init; } = TimeSpan.FromSeconds(10);
}
