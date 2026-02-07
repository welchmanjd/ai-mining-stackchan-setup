using System;
using System.IO;
using System.Security.Cryptography;

namespace AiStackchanSetup.Models;

public sealed class FirmwareInfo
{
    public string Path { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastWriteTime { get; init; }
    public string Sha256 { get; init; } = string.Empty;

    public static FirmwareInfo? FromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new FileInfo(path);
        return new FirmwareInfo
        {
            Path = path,
            Size = info.Length,
            LastWriteTime = info.LastWriteTime,
            Sha256 = ComputeSha256(path)
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
