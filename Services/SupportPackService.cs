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
    public async Task<string> CreateSupportPackAsync(SupportSummary summary)
    {
        Directory.CreateDirectory(LogService.LogDirectory);

        var zipPath = Path.Combine(LogService.LogDirectory, $"support_pack_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        try
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            AddLatestAppLog(zip);
            AddFileIfExists(zip, LogService.FlashLogPath, "flash_esptool.log");
            AddFileIfExists(zip, LogService.DeviceLogPath, "device_log.txt");

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

    private static void AddLatestAppLog(ZipArchive zip)
    {
        var dir = LogService.LogDirectory;
        var latest = Directory.GetFiles(dir, "app-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(latest))
        {
            zip.CreateEntryFromFile(latest, "app.log");
        }
        else
        {
            var fallback = Path.Combine(dir, "app.log");
            AddFileIfExists(zip, fallback, "app.log");
        }
    }

    private static void AddFileIfExists(ZipArchive zip, string path, string entryName)
    {
        if (File.Exists(path))
        {
            zip.CreateEntryFromFile(path, entryName);
        }
    }
}
