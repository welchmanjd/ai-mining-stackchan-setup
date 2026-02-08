using System;

namespace AiStackchanSetup.Services;

internal sealed class SerialCommandException : Exception
{
    public SerialCommandException(string reason, string line)
        : base(reason)
    {
        Reason = reason;
        Line = line;
    }

    public string Reason { get; }
    public string Line { get; }
}
