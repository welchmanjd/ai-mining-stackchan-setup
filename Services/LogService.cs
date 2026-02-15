using System;
using System.IO;
using System.Linq;
using Serilog;

namespace AiStackchanSetup.Services;

public static class LogService
{
    public static readonly string LogDirectory = ResolveLogDirectory();

    public static readonly string AppLogPath = Path.Combine(LogDirectory, "app.log");
    public static readonly string FlashLogPath = Path.Combine(LogDirectory, "flash_esptool.log");
    public static readonly string DeviceLogPath = Path.Combine(LogDirectory, "device_log.txt");
    public static readonly string SerialLogPath = Path.Combine(LogDirectory, "serial_comm.log");

    private static string ResolveLogDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("AISTACKCHAN_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        try
        {
            // Packaged distribution runs from "<dist>/app", so parent becomes "<dist>".
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var distRoot = Directory.GetParent(baseDir)?.FullName;
            if (!string.IsNullOrWhiteSpace(distRoot))
            {
                return Path.Combine(distRoot, "log");
            }
        }
        catch
        {
            // fallback below
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AiStackchanSetup",
            "Logs");
    }

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

        Log.Information("app.lifecycle.started");
    }

    public static void Shutdown()
    {
        Log.Information("app.lifecycle.stopping");
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
