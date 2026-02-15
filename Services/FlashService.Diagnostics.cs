using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

public partial class FlashService
{
    private static void AppendFlashContext(StringBuilder output, string toolName, string portName, int baud, bool erase, string firmwarePath)
    {
        output.AppendLine("=== flash context ===");
        output.AppendLine($"timestamp: {DateTimeOffset.Now:O}");
        output.AppendLine($"tool: {toolName}");
        output.AppendLine($"port: {portName}");
        output.AppendLine($"baud: {baud}");
        output.AppendLine($"erase: {erase}");
        output.AppendLine($"firmware: {firmwarePath}");

        if (File.Exists(firmwarePath))
        {
            var info = new FileInfo(firmwarePath);
            output.AppendLine($"firmware_size: {info.Length}");
            output.AppendLine($"firmware_mtime: {info.LastWriteTime:O}");
            output.AppendLine($"firmware_sha256: {ComputeSha256(firmwarePath)}");
        }
    }

    private static async Task AppendEspflashDiagnosticsAsync(
        StringBuilder output,
        string exePath,
        string portName,
        int baud,
        CancellationToken token)
    {
        output.AppendLine();
        output.AppendLine("=== espflash diagnostics ===");
        await AppendCommandProbeAsync(output, exePath, "--version", token);
        await AppendCommandProbeAsync(output, exePath, "list-ports", token);
        await AppendCommandProbeAsync(output, exePath, $"board-info --non-interactive -c esp32 -p {portName} -B {baud}", token);
        if (baud != 115200)
        {
            await AppendCommandProbeAsync(output, exePath, $"board-info --non-interactive -c esp32 -p {portName} -B 115200", token);
        }
    }

    private static async Task TryEnterBootloaderAsync(string portName, StringBuilder output, CancellationToken token)
    {
        try
        {
            using var serial = new SerialPort(portName, 115200)
            {
                ReadTimeout = 200,
                WriteTimeout = 200,
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false
            };
            serial.Open();

            // Common ESP32 auto-program sequence via DTR/RTS.
            serial.DtrEnable = true;   // IO0 low (on typical adapters)
            serial.RtsEnable = true;   // EN low
            await Task.Delay(120, token);
            serial.RtsEnable = false;  // EN high
            await Task.Delay(120, token);
            serial.DtrEnable = false;  // IO0 high (normal run)
            await Task.Delay(80, token);

            output.AppendLine("bootloader_pulse: ok");
        }
        catch (Exception ex)
        {
            output.AppendLine($"bootloader_pulse: skipped ({ex.Message})");
        }
    }

    private static async Task AppendCommandProbeAsync(
        StringBuilder output,
        string exePath,
        string arguments,
        CancellationToken token)
    {
        output.AppendLine($"> probe: {Path.GetFileName(exePath)} {arguments}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(timeoutCts.Token);

            output.AppendLine($"exit: {process.ExitCode}");
            if (!string.IsNullOrWhiteSpace(stdoutTask.Result))
            {
                output.AppendLine(stdoutTask.Result);
            }
            if (!string.IsNullOrWhiteSpace(stderrTask.Result))
            {
                output.AppendLine(stderrTask.Result);
            }
        }
        catch (OperationCanceledException)
        {
            if (process != null)
            {
                TryKillProcess(process, output, token.IsCancellationRequested ? "probe-cancelled" : "probe-timeout");
            }
            output.AppendLine(token.IsCancellationRequested ? "probe cancelled" : "probe timeout");
        }
        catch (Exception ex)
        {
            output.AppendLine($"probe failed: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static async Task<FlashResult> FailWithLogAsync(
        string message,
        string toolName,
        string portName,
        int baud,
        bool erase,
        string firmwarePath,
        CancellationToken token)
    {
        var logPath = LogService.FlashLogPath;
        var output = new StringBuilder();
        AppendFlashContext(output, toolName, portName, baud, erase, firmwarePath);
        output.AppendLine(message);
        await File.WriteAllTextAsync(logPath, output.ToString(), token);

        return new FlashResult
        {
            Success = false,
            ExitCode = -1,
            Message = message,
            LogPath = logPath
        };
    }
}
