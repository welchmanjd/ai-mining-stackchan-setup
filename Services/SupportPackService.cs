using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

            await AddLatestAppLogAsync(zip, config);
            await AddFileIfExistsRedactedAsync(zip, LogService.FlashLogPath, "flash_esptool.log", config);
            await AddFileIfExistsRedactedAsync(zip, LogService.DeviceLogPath, "device_log.txt", config);
            await AddFileIfExistsRedactedAsync(zip, LogService.SerialLogPath, "serial_comm.log", config);

            var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            var entry = zip.CreateEntry("summary.json");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(summaryJson);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Support pack creation failed");
        }

        return zipPath;
    }

    private static async Task AddLatestAppLogAsync(ZipArchive zip, DeviceConfig config)
    {
        var dir = LogService.LogDirectory;
        var latest = Directory.GetFiles(dir, "app-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(latest))
        {
            await AddFileIfExistsRedactedAsync(zip, latest, "app.log", config);
        }
        else
        {
            var fallback = Path.Combine(dir, "app.log");
            await AddFileIfExistsRedactedAsync(zip, fallback, "app.log", config);
        }
    }

    private static async Task AddFileIfExistsRedactedAsync(ZipArchive zip, string path, string entryName, DeviceConfig config)
    {
        if (File.Exists(path))
        {
            var raw = await File.ReadAllTextAsync(path);
            var sanitized = AiStackchanSetup.Infrastructure.SensitiveDataRedactor.Redact(raw, config);
            var entry = zip.CreateEntry(entryName);
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(sanitized);
        }
    }
}
