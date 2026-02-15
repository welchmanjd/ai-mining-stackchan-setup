using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public partial class FlashService
{
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

            await TryEnterBootloaderAsync(portName, output, token);

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
                var missingToolsMessage = "PlatformIO の Python / esptool.py が見つかりません。開発環境の PlatformIO を確認してください。";
                output.AppendLine(missingToolsMessage);
                await File.WriteAllTextAsync(logPath, output.ToString(), token);
                return new FlashResult
                {
                    Success = false,
                    ExitCode = -1,
                    Message = missingToolsMessage,
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

            await TryEnterBootloaderAsync(portName, output, token);

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
            var combined = $"{stdoutTask.Result}\n{stderrTask.Result}";
            var message = "OK";
            if (combined.Contains("No serial data received.", StringComparison.OrdinalIgnoreCase))
            {
                message = "Failed to connect to ESP32: No serial data received.";
            }
            else if (!success && !string.IsNullOrWhiteSpace(stderrTask.Result))
            {
                message = stderrTask.Result.Trim();
            }
            else if (!success && !string.IsNullOrWhiteSpace(stdoutTask.Result))
            {
                message = stdoutTask.Result.Trim();
            }

            if (!success)
            {
                Log.Warning("esptool exit code {Code}", process.ExitCode);
            }

            return new FlashResult
            {
                Success = success,
                ExitCode = process.ExitCode,
                Message = message,
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
}
