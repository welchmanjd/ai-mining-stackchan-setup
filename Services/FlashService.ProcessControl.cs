using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AiStackchanSetup.Services;

public partial class FlashService
{
    public void KillActiveProcesses()
    {
        Process[] snapshot;
        lock (_activeProcessesLock)
        {
            snapshot = _activeProcesses.ToArray();
        }

        foreach (var process in snapshot)
        {
            TryKillProcess(process, null, "app-shutdown");
        }
    }

    private void RegisterActiveProcess(Process process)
    {
        lock (_activeProcessesLock)
        {
            _activeProcesses.Add(process);
        }
    }

    private void UnregisterActiveProcess(Process process)
    {
        lock (_activeProcessesLock)
        {
            _activeProcesses.Remove(process);
        }
    }

    private static void TryKillProcess(Process process, StringBuilder? output, string reason)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            output?.AppendLine($"Process killed ({reason}).");
        }
        catch (Exception ex)
        {
            output?.AppendLine($"Process kill failed ({reason}): {ex.Message}");
        }
    }
}
