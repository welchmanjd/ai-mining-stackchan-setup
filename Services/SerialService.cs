using System;
using System.Text;

namespace AiStackchanSetup.Services;

public partial class SerialService : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    public int BaudRate { get; set; } = 115200;
    private const string EmptyValueSentinel = "__MC_EMPTY__";
}
