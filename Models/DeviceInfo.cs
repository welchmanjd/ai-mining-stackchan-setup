using System.Text.Json;

namespace AiStackchanSetup.Models;

public sealed class DeviceInfo
{
    public string App { get; init; } = string.Empty;
    public string Ver { get; init; } = string.Empty;
    public int Baud { get; init; }

    public static DeviceInfo? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var app = ReadString(doc.RootElement, "app");
            var ver = ReadString(doc.RootElement, "ver");
            var baud = ReadInt(doc.RootElement, "baud");
            return new DeviceInfo
            {
                App = app,
                Ver = ver,
                Baud = baud
            };
        }
        catch
        {
            return null;
        }
    }

    public string ToSummary()
    {
        var baudText = Baud > 0 ? Baud.ToString() : "unknown";
        return $"app={App} / ver={Ver} / baud={baudText}";
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var prop) ? prop.ToString() : string.Empty;
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetInt32(),
            JsonValueKind.String when int.TryParse(prop.GetString(), out var value) => value,
            _ => 0
        };
    }
}
