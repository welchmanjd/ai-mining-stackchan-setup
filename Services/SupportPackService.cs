using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class SupportPackService
{
    public async Task<string> CreateSupportPackAsync(SupportSummary summary, DeviceConfig config)
    {
        Directory.CreateDirectory(LogService.LogDirectory);

        var zipPath = Path.Combine(LogService.LogDirectory, $"support_pack_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        try
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            {
                var entry = zip.CreateEntry("summary.json");
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(summaryJson);
            }

            await AddLatestAppLogAsync(zip, config);
            await AddFileIfExistsRedactedAsync(zip, LogService.FlashLogPath, "flash_esptool.log", config);
            var latestDeviceLog = LogService.GetLatestDeviceLogPath();
            if (!string.IsNullOrWhiteSpace(latestDeviceLog))
            {
                await AddFileIfExistsRedactedAsync(zip, latestDeviceLog, "device_log.txt", config);
            }
            else
            {
                await AddFileIfExistsRedactedAsync(zip, LogService.DeviceLogPath, "device_log.txt", config);
            }
            await AddFileIfExistsRedactedAsync(zip, LogService.SerialLogPath, "serial_comm.log", config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Support pack creation failed");
        }

        return zipPath;
    }

    private static async Task AddLatestAppLogAsync(ZipArchive zip, DeviceConfig config)
    {
        await AddFileIfExistsRedactedAsync(zip, LogService.AppLogPath, "app.log", config);
    }

    private static async Task AddFileIfExistsRedactedAsync(ZipArchive zip, string path, string entryName, DeviceConfig config)
    {
        if (File.Exists(path))
        {
            var raw = await ReadAllTextSharedAsync(path);
            var sanitized = AiStackchanSetup.Infrastructure.SensitiveDataRedactor.Redact(raw, config);
            var entry = zip.CreateEntry(entryName);
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(sanitized);
            return;
        }
        var missingEntry = zip.CreateEntry(entryName);
        await using var missingStream = missingEntry.Open();
        await using var missingWriter = new StreamWriter(missingStream);
        await missingWriter.WriteAsync($"Log file not found: {path}");
    }

    private static async Task<string> ReadAllTextSharedAsync(string path)
    {
        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read log file {Path}", path);
            return $"Failed to read log file: {path}";
        }
    }
}
