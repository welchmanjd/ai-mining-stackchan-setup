using System.IO;
using System.Text.Json;

namespace AiStackchanSetup.Models;

public sealed class FirmwareManifest
{
    public string App { get; init; } = string.Empty;
    public string Ver { get; init; } = string.Empty;
    public string BuildId { get; init; } = string.Empty;

    public static FirmwareManifest? FromFirmwarePath(string firmwarePath)
    {
        if (string.IsNullOrWhiteSpace(firmwarePath))
        {
            return null;
        }

        var manifestPath = Path.ChangeExtension(firmwarePath, ".meta.json");
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new FirmwareManifest
            {
                App = ReadString(root, "app"),
                Ver = ReadString(root, "ver"),
                BuildId = ReadString(root, "build_id")
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var prop) ? prop.ToString() : string.Empty;
    }
}
