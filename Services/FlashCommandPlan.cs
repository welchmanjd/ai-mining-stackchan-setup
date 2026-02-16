using System.Collections.Generic;
using System.Linq;

namespace AiStackchanSetup.Services;

internal static class FlashCommandPlan
{
    public static IReadOnlyList<int> BuildBaudCandidates(int preferredBaud)
    {
        return new[] { preferredBaud, 460800, 115200 }
            .Distinct()
            .ToArray();
    }

    public static string BuildEsptoolEraseArgs(string portName, int baud)
    {
        return $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud {baud} erase_flash";
    }

    public static string BuildEsptoolWriteArgs(string portName, int baud, string firmwarePath, bool noStub)
    {
        var noStubArg = noStub ? "--no-stub " : string.Empty;
        return $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud {baud} {noStubArg}write_flash -z 0x0 \"{firmwarePath}\"";
    }

    public static string BuildEspflashEraseArgs(string portName)
    {
        return $"erase-flash --non-interactive -c esp32 -p {portName}";
    }

    public static string BuildEspflashWriteArgs(string portName, int baud, string firmwarePath)
    {
        return $"write-bin --non-interactive -c esp32 -p {portName} -B {baud} 0x0 \"{firmwarePath}\"";
    }
}