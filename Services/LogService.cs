using System;
using System.IO;
using Serilog;

namespace AiStackchanSetup.Services;

public static class LogService
{
    public static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiStackchanSetup",
        "Logs");

    public static readonly string AppLogRollingPath = Path.Combine(LogDirectory, "app-.log");
    public static readonly string FlashLogPath = Path.Combine(LogDirectory, "flash_esptool.log");
    public static readonly string DeviceLogPath = Path.Combine(LogDirectory, "device_log.txt");
    public static readonly string SerialLogPath = Path.Combine(LogDirectory, "serial_comm.log");

    public static void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(AppLogRollingPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
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
}
