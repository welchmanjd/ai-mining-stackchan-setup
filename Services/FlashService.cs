using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class FlashService
{
    public string EsptoolPath { get; set; } = "esptool.exe";

    public async Task<FlashResult> FlashAsync(string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        Directory.CreateDirectory(LogService.LogDirectory);

        if (erase)
        {
            var eraseResult = await RunEsptoolAsync($"--chip esp32 --port {portName} --baud {baud} erase_flash", token);
            if (!eraseResult.Success)
            {
                eraseResult.Message = "erase_flash 失敗";
                return eraseResult;
            }
        }

        var args = $"--chip esp32 --port {portName} --baud {baud} write_flash -z 0x0 \"{firmwarePath}\"";
        var result = await RunEsptoolAsync(args, token);
        result.Message = result.Success ? "書き込み成功" : "書き込み失敗";
        return result;
    }

    private async Task<FlashResult> RunEsptoolAsync(string arguments, CancellationToken token)
    {
        var logPath = LogService.FlashLogPath;
        var output = new StringBuilder();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = EsptoolPath,
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
}
