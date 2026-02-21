using System;

namespace AiStackchanSetup.Steps;

public sealed class StepTimeouts
{
    public TimeSpan PortDetect { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan Flash { get; init; } = TimeSpan.FromMinutes(3);
    public TimeSpan Hello { get; init; } = TimeSpan.FromSeconds(12);
    public TimeSpan SendConfig { get; init; } = TimeSpan.FromSeconds(45);
    public TimeSpan ApplyConfig { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan LongRunningNoticeDelay { get; init; } = TimeSpan.FromSeconds(15);
}
