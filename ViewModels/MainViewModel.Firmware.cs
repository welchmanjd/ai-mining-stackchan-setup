using System;
using System.IO;
using System.Linq;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: firmware path discovery and firmware info/comparison helpers.
    private static string ResolveDefaultFirmwarePath()
    {
        var exeDir = GetExecutableDirectory();
        if (string.IsNullOrWhiteSpace(exeDir))
        {
            return string.Empty;
        }

        // Assume parent of "app" directory (distribution zip root).
        string? rootDir = null;
        try
        {
            rootDir = Directory.GetParent(exeDir)?.FullName;
        }
        catch
        {
            rootDir = null;
        }

        // Search only root-level firmware as default target.
        var candidates = new[]
        {
            rootDir != null ? Path.Combine(rootDir, "firmware", "stackchan_core2_public.bin") : null,
        };

        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
            {
                return c!;
            }
        }

        // Fallback: pick a .bin containing "_public" even if filename changes.
        var searchDirs = new[]
        {
            rootDir != null ? Path.Combine(rootDir, "firmware") : null,
        };

        foreach (var d in searchDirs)
        {
            if (string.IsNullOrWhiteSpace(d) || !Directory.Exists(d))
            {
                continue;
            }

            try
            {
                var f = Directory.EnumerateFiles(d, "*.bin", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(p => Path.GetFileName(p).Contains("_public", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(f) && File.Exists(f))
                {
                    return f!;
                }
            }
            catch
            {
                // ignore
            }
        }

        return string.Empty;
    }

    private static string? GetExecutableDirectory()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        return Path.GetDirectoryName(exePath);
    }

    public void UpdateCurrentFirmwareInfo(DeviceInfo? info)
    {
        if (info == null)
        {
            CurrentFirmwareInfoText = "未取得";
            RefreshFirmwareComparisonMessage();
            return;
        }

        var app = string.IsNullOrWhiteSpace(info.App) ? "unknown" : info.App;
        var ver = string.IsNullOrWhiteSpace(info.Ver) ? "unknown" : info.Ver;
        var build = string.IsNullOrWhiteSpace(info.BuildId) ? "unknown" : info.BuildId;
        CurrentFirmwareInfoText = $"app={app} / ver={ver} / build={build}";
        RefreshFirmwareComparisonMessage();
    }

    private void RefreshFirmwareComparisonMessage()
    {
        var manifest = FirmwareManifest.FromFirmwarePath(FirmwarePath);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Ver))
        {
            FirmwareCompareMessage = "";
            return;
        }

        var currentVer = ExtractToken(CurrentFirmwareInfoText, "ver=");
        if (string.IsNullOrWhiteSpace(currentVer) || currentVer.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            FirmwareCompareMessage = "";
            return;
        }

        if (string.Equals(currentVer, manifest.Ver, StringComparison.OrdinalIgnoreCase))
        {
            FirmwareCompareMessage = "同じバージョンのファームウェアが既に書き込まれています。必要なら上書きできます。";
            return;
        }

        FirmwareCompareMessage = $"現在 ver={currentVer} / 書込 ver={manifest.Ver}";
    }

    private static string ExtractToken(string text, string prefix)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var i = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            return string.Empty;
        }

        var start = i + prefix.Length;
        var end = text.IndexOf(" / ", start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = text.Length;
        }

        return text.Substring(start, end - start).Trim();
    }

    private static string BuildFirmwareInfoText(string path)
    {
        var info = FirmwareInfo.FromFile(path);
        if (info == null)
        {
            return "未検出";
        }
        var manifest = FirmwareManifest.FromFirmwarePath(path);
        if (manifest == null)
        {
            return $"size={info.Size} bytes / mtime={info.LastWriteTime:yyyy-MM-dd HH:mm:ss} / sha256={info.Sha256[..12]}";
        }

        var ver = string.IsNullOrWhiteSpace(manifest.Ver) ? "unknown" : manifest.Ver;
        var build = string.IsNullOrWhiteSpace(manifest.BuildId) ? "unknown" : manifest.BuildId;
        return $"ver={ver} / build={build} / size={info.Size} bytes / mtime={info.LastWriteTime:yyyy-MM-dd HH:mm:ss} / sha256={info.Sha256[..12]}";
    }

}
