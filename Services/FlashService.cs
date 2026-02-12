using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class FlashService
{
    private readonly object _activeProcessesLock = new();
    private readonly HashSet<Process> _activeProcesses = new();

    public string? PlatformIoHome { get; set; }

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
    public async Task<FlashResult> FlashAsync(string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        Directory.CreateDirectory(LogService.LogDirectory);

        if (!File.Exists(firmwarePath))
        {
            return await FailWithLogAsync(
                "ファームウェアファイルが見つかりません。Resources/firmware を確認してください。",
                "none",
                portName,
                baud,
                erase,
                firmwarePath,
                token);
        }

        // Prefer esptool.py first for compatibility with older stable behavior.
        if (IsEsptoolAvailable())
        {
            if (erase)
            {
                var eraseResult = await RunEsptoolAsync($"--chip esp32 --port {portName} --baud {baud} erase_flash", portName, baud, erase, firmwarePath, token);
                if (!eraseResult.Success)
                {
                    eraseResult.Message = "erase_flash 失敗";
                    return eraseResult;
                }
            }

            var argsPrimary = $"--chip esp32 --port {portName} --baud {baud} write_flash -z 0x0 \"{firmwarePath}\"";
            var esptoolPrimary = await RunEsptoolAsync(argsPrimary, portName, baud, erase, firmwarePath, token);
            esptoolPrimary.Message = esptoolPrimary.Success ? "書き込み成功 (esptool)" : "書き込み失敗 (esptool)";
            return esptoolPrimary;
        }

        // Try to use bundled espflash first
        var espFlashPath = ResolveEspFlashPath();
        if (!string.IsNullOrWhiteSpace(espFlashPath))
        {
            var espflashUnavailableForThisRun = false;

            if (erase)
            {
                var eraseArgs = $"erase-flash --non-interactive -c esp32 -p {portName}";
                var eraseResult = await RunToolAsync(espFlashPath, eraseArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (!eraseResult.Success)
                {
                    if (IsEsptoolAvailable())
                    {
                        Log.Warning("espflash erase failed. Falling back to esptool.py erase_flash");
                        var eraseFallback = await RunEsptoolAsync($"--chip esp32 --port {portName} --baud {baud} erase_flash", portName, baud, erase, firmwarePath, token);
                        if (!eraseFallback.Success)
                        {
                            eraseFallback.Message = "erase_flash 失敗 (espflash + esptool)";
                            return eraseFallback;
                        }
                        espflashUnavailableForThisRun = true;
                    }
                    else
                    {
                        eraseResult.Message = "espflash erase-flash 失敗";
                        return eraseResult;
                    }
                }
            }

            // Note: --no-stub might be needed for some usb-serial chips but usually standard flash is fine.
            // Specifying offset 0x0 is implied for single bin if not specified? 
            // espflash flash -p COMx -b 921600 file.bin addresses 0x0 by default for raw binaries provided as argument? 
            // Actually espflash usually expects a partition table or specific format. 
            // But if we give it a raw bin, we might need to specify address.
            // espflash write-bin 0x0 file.bin is the command for raw binaries in older versions, 
            // or `flash` command might strictly require partition table.
            // Let's check typical usage. "write-bin" is explicit.
            // However, esptool command was `write_flash -z 0x0`.
            // Let's try `write-bin 0x0` if `espflash` supports it, or `flash` regarding user's tool version.
            // Assuming modern espflash: `write-bin -p {port} -B {baud} 0x0 {firmware}`
            
            if (!espflashUnavailableForThisRun)
            {
                var flashArgs = $"write-bin --non-interactive -c esp32 -p {portName} -B {baud} 0x0 \"{firmwarePath}\"";
                var result = await RunToolAsync(espFlashPath, flashArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (result.Success)
                {
                    result.Message = "書き込み成功 (espflash)";
                    return result;
                }

                if (IsEsptoolAvailable())
                {
                    Log.Warning("espflash failed. Falling back to esptool.py");
                    var fallbackArgs = $"--chip esp32 --port {portName} --baud {baud} write_flash -z 0x0 \"{firmwarePath}\"";
                    var fallback = await RunEsptoolAsync(fallbackArgs, portName, baud, erase, firmwarePath, token);
                    fallback.Message = fallback.Success ? "書き込み成功 (esptool fallback)" : "書き込み失敗 (espflash + esptool)";
                    return fallback;
                }

                result.Message = "書き込み失敗 (espflash)";
                return result;
            }

            if (IsEsptoolAvailable())
            {
                Log.Warning("Skipping espflash write due to prior espflash failure. Using esptool.py directly.");
                var fallbackArgs = $"--chip esp32 --port {portName} --baud {baud} write_flash -z 0x0 \"{firmwarePath}\"";
                var fallback = await RunEsptoolAsync(fallbackArgs, portName, baud, erase, firmwarePath, token);
                fallback.Message = fallback.Success ? "書き込み成功 (esptool direct)" : "書き込み失敗 (esptool direct)";
                return fallback;
            }

            return await FailWithLogAsync(
                "espflash が接続できず、esptool.py も利用できません。",
                "none",
                portName,
                baud,
                erase,
                firmwarePath,
                token);
        }

        return await FailWithLogAsync(
            "書き込みツールが見つかりません。esptool.py または espflash.exe を確認してください。",
            "none",
            portName,
            baud,
            erase,
            firmwarePath,
            token);
    }

    private string? ResolveEspFlashPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = Path.Combine(baseDir, "tools", "espflash.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private async Task<FlashResult> RunToolAsync(string exePath, string arguments, string toolName, string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        var logPath = LogService.FlashLogPath;
        var output = new StringBuilder();
        Process? process = null;

        try
        {
            AppendFlashContext(output, toolName, portName, baud, erase, firmwarePath);
            output.AppendLine($"exe: {exePath}");
            output.AppendLine($"args: {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();
            RegisterActiveProcess(process);
            using var registration = token.Register(() => TryKillProcess(process, output, "cancellation"));

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(token);

            output.AppendLine(stdoutTask.Result);
            output.AppendLine(stderrTask.Result);

            var success = process.ExitCode == 0;
            if (!success && string.Equals(toolName, "espflash", StringComparison.OrdinalIgnoreCase))
            {
                await AppendEspflashDiagnosticsAsync(output, exePath, portName, baud, token);
            }

            await File.WriteAllTextAsync(logPath, output.ToString(), token);

            if (!success)
            {
                Log.Warning("{Tool} exit code {Code}", toolName, process.ExitCode);
            }

            return new FlashResult
            {
                Success = success,
                ExitCode = process.ExitCode,
                LogPath = logPath
            };
        }
        catch (OperationCanceledException)
        {
            output.AppendLine("Process cancelled.");
            await File.WriteAllTextAsync(logPath, output.ToString(), token);
            return new FlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = "キャンセルされました",
                LogPath = logPath
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Tool} execution failed", toolName);
            await File.WriteAllTextAsync(logPath, output.ToString(), token);
            return new FlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = ex.Message,
                LogPath = logPath
            };
        }
        finally
        {
            if (process != null)
            {
                UnregisterActiveProcess(process);
                process.Dispose();
            }
        }
    }

    private async Task<FlashResult> RunEsptoolAsync(string arguments, string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        var logPath = LogService.FlashLogPath;
        var output = new StringBuilder();
        Process? process = null;

        try
        {
            AppendFlashContext(output, "esptool.py", portName, baud, erase, firmwarePath);
            output.AppendLine($"args: {arguments}");

            var pythonPath = ResolvePythonPath();
            var esptoolPyPath = ResolveEsptoolPyPath();
            output.AppendLine($"python: {pythonPath ?? "not found"}");
            output.AppendLine($"esptool.py: {esptoolPyPath ?? "not found"}");
            if (string.IsNullOrWhiteSpace(pythonPath) || string.IsNullOrWhiteSpace(esptoolPyPath))
            {
                var message = "PlatformIO の Python / esptool.py が見つかりません。開発環境の PlatformIO を確認してください。";
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

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{esptoolPyPath}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();
            RegisterActiveProcess(process);
            using var registration = token.Register(() => TryKillProcess(process, output, "cancellation"));

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(token);

            output.AppendLine(stdoutTask.Result);
            output.AppendLine(stderrTask.Result);
            await File.WriteAllTextAsync(logPath, output.ToString(), token);

            var success = process.ExitCode == 0;
            if (!success)
            {
                Log.Warning("esptool exit code {Code}", process.ExitCode);
            }

            return new FlashResult
            {
                Success = success,
                ExitCode = process.ExitCode,
                LogPath = logPath
            };
        }
        catch (OperationCanceledException)
        {
            output.AppendLine("Process cancelled.");
            await File.WriteAllTextAsync(logPath, output.ToString(), token);
            return new FlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = "キャンセルされました",
                LogPath = logPath
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "esptool execution failed");
            await File.WriteAllTextAsync(logPath, output.ToString(), token);
            return new FlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = ex.Message,
                LogPath = logPath
            };
        }
        finally
        {
            if (process != null)
            {
                UnregisterActiveProcess(process);
                process.Dispose();
            }
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

    private string? ResolvePythonPath()
    {
        var home = GetPlatformIoHome();
        var candidate = Path.Combine(home, "penv", "Scripts", "python.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private string? ResolveEsptoolPyPath()
    {
        var home = GetPlatformIoHome();
        var candidate = Path.Combine(home, "packages", "tool-esptoolpy", "esptool.py");
        return File.Exists(candidate) ? candidate : null;
    }

    private string GetPlatformIoHome()
    {
        if (!string.IsNullOrWhiteSpace(PlatformIoHome))
        {
            return PlatformIoHome;
        }

        var env = Environment.GetEnvironmentVariable("PLATFORMIO_HOME_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".platformio");
    }

    private bool IsEsptoolAvailable()
    {
        var pythonPath = ResolvePythonPath();
        var esptoolPyPath = ResolveEsptoolPyPath();
        return !string.IsNullOrWhiteSpace(pythonPath) && !string.IsNullOrWhiteSpace(esptoolPyPath);
    }
}
