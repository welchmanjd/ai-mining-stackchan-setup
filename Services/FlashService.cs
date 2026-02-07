using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class FlashService
{
    public string? PlatformIoHome { get; set; }
    public string EspflashPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "tools", "espflash.exe");

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

        var tool = SelectFlasherTool();
        if (tool == FlasherTool.None)
        {
            return await FailWithLogAsync(
                "書き込みツールが見つかりません。tools\\espflash.exe を同梱するか、PlatformIO を確認してください。",
                "none",
                portName,
                baud,
                erase,
                firmwarePath,
                token);
        }

        if (tool == FlasherTool.Espflash)
        {
            if (erase)
            {
                var eraseResult = await RunEspflashAsync(BuildEspflashEraseArgs(portName, baud), portName, baud, erase, firmwarePath, token);
                if (!eraseResult.Success)
                {
                    eraseResult.Message = "erase_flash 失敗";
                    return eraseResult;
                }
            }

            var result = await RunEspflashAsync(BuildEspflashWriteArgs(portName, baud, firmwarePath), portName, baud, erase, firmwarePath, token);
            result.Message = result.Success ? "書き込み成功" : "書き込み失敗";
            return result;
        }

        if (erase)
        {
            var eraseResult = await RunEsptoolAsync($"--chip esp32 --port {portName} --baud {baud} erase_flash", portName, baud, erase, firmwarePath, token);
            if (!eraseResult.Success)
            {
                eraseResult.Message = "erase_flash 失敗";
                return eraseResult;
            }
        }

        var args = $"--chip esp32 --port {portName} --baud {baud} write_flash -z 0x0 \"{firmwarePath}\"";
        var esptoolResult = await RunEsptoolAsync(args, portName, baud, erase, firmwarePath, token);
        esptoolResult.Message = esptoolResult.Success ? "書き込み成功" : "書き込み失敗";
        return esptoolResult;
    }

    private async Task<FlashResult> RunEsptoolAsync(string arguments, string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        var logPath = LogService.FlashLogPath;
        var output = new StringBuilder();

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

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();

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
    }

    private async Task<FlashResult> RunEspflashAsync(string arguments, string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        var logPath = LogService.FlashLogPath;
        var output = new StringBuilder();

        try
        {
            AppendFlashContext(output, "espflash.exe", portName, baud, erase, firmwarePath);
            output.AppendLine($"espflash: {EspflashPath}");
            output.AppendLine($"args: {arguments}");
            await AppendEspflashDiagnosticsAsync(output, token);

            var startInfo = new ProcessStartInfo
            {
                FileName = EspflashPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask);
            try
            {
                await process.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process, output, "espflash canceled");
                throw;
            }

            output.AppendLine(stdoutTask.Result);
            output.AppendLine(stderrTask.Result);
            await File.WriteAllTextAsync(logPath, output.ToString(), token);

            return new FlashResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                LogPath = logPath
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "espflash execution failed");
            await File.WriteAllTextAsync(logPath, output.ToString(), token);
            return new FlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = ex.Message,
                LogPath = logPath
            };
        }
    }

    private async Task AppendEspflashDiagnosticsAsync(StringBuilder output, CancellationToken token)
    {
        var version = await RunEspflashProbeAsync("--version", token);
        output.AppendLine("=== espflash --version ===");
        output.AppendLine(version);

        var help = await RunEspflashProbeAsync("write-bin --help", token);
        output.AppendLine("=== espflash write-bin --help ===");
        output.AppendLine(help);
    }

    private async Task<string> RunEspflashProbeAsync(string arguments, CancellationToken token)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = EspflashPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask);
            try
            {
                await process.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process, null, "espflash probe canceled");
                return "canceled";
            }

            return $"{stdoutTask.Result}{Environment.NewLine}{stderrTask.Result}".Trim();
        }
        catch (Exception ex)
        {
            return $"probe failed: {ex.Message}";
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

    private static string BuildEspflashWriteArgs(string portName, int baud, string firmwarePath)
    {
        return $"write-bin --port \"{portName}\" --baud {baud} 0x0 \"{firmwarePath}\"";
    }

    private static string BuildEspflashEraseArgs(string portName, int baud)
    {
        return $"erase-flash --port \"{portName}\" --baud {baud}";
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

    private FlasherTool SelectFlasherTool()
    {
        if (File.Exists(EspflashPath))
        {
            return FlasherTool.Espflash;
        }

        var pythonPath = ResolvePythonPath();
        var esptoolPyPath = ResolveEsptoolPyPath();
        if (!string.IsNullOrWhiteSpace(pythonPath) && !string.IsNullOrWhiteSpace(esptoolPyPath))
        {
            return FlasherTool.Esptool;
        }

        return FlasherTool.None;
    }

    private enum FlasherTool
    {
        None,
        Espflash,
        Esptool
    }
}
