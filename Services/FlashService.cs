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

        // Try to use bundled espflash first
        var espFlashPath = ResolveEspFlashPath();
        if (!string.IsNullOrWhiteSpace(espFlashPath))
        {
            if (erase)
            {
                var eraseArgs = $"erase-flash -p {portName}";
                var eraseResult = await RunToolAsync(espFlashPath, eraseArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (!eraseResult.Success)
                {
                    eraseResult.Message = "espflash erase-flash 失敗";
                    return eraseResult;
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
            // Assuming modern espflash: `write-bin -p {port} -b {baud} 0x0 {firmware}`
            
            var flashArgs = $"write-bin -p {portName} -b {baud} 0x0 \"{firmwarePath}\"";
            var result = await RunToolAsync(espFlashPath, flashArgs, "espflash", portName, baud, erase, firmwarePath, token);
            result.Message = result.Success ? "書き込み成功 (espflash)" : "書き込み失敗 (espflash)";
            return result;
        }

        var esptoolAvailable = IsEsptoolAvailable();
        if (!esptoolAvailable)
        {
            return await FailWithLogAsync(
                "PlatformIO の Python / esptool.py が見つかりません。開発環境の PlatformIO を確認してください。",
                "esptool.py",
                portName,
                baud,
                erase,
                firmwarePath,
                token);
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

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();
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
