using System;
using System.IO;
using System.Linq;
using Serilog;

namespace AiStackchanSetup.Services;

public static class LogService
{
    public static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiStackchanSetup",
        "Logs");

    public static readonly string AppLogPath = Path.Combine(LogDirectory, "app.log");
    public static readonly string FlashLogPath = Path.Combine(LogDirectory, "flash_esptool.log");
    public static readonly string DeviceLogPath = Path.Combine(LogDirectory, "device_log.txt");
    public static readonly string SerialLogPath = Path.Combine(LogDirectory, "serial_comm.log");

    public static void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);
        ClearSessionLogs();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(AppLogPath,
                shared: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level:u3} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("App started");
    }

    public static void Shutdown()
    {
        Log.Information("App shutting down");
        Log.CloseAndFlush();
    }

    private static void ClearSessionLogs()
    {
        TryDelete(SerialLogPath);
        TryDelete(FlashLogPath);

        TryDelete(AppLogPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public static string CreateDeviceLogPath()
    {
        Directory.CreateDirectory(LogDirectory);
        return Path.Combine(LogDirectory, $"device_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
    }

    public static string? GetLatestDeviceLogPath()
    {
        if (!Directory.Exists(LogDirectory))
        {
            return null;
        }

        var latest = Directory.GetFiles(LogDirectory, "device_log_*.txt")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(latest))
        {
            return latest;
        }

        return File.Exists(DeviceLogPath) ? DeviceLogPath : null;
    }
}
