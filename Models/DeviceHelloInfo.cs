using System.Collections.Generic;
using System.Text.Json;

namespace AiStackchanSetup.Models;

public sealed class DeviceHelloInfo
{
    public string FirmwareVersion { get; init; } = string.Empty;
    public string Board { get; init; } = string.Empty;
    public string WifiStatus { get; init; } = string.Empty;
    public string LastError { get; init; } = string.Empty;
    public string CanReceiveConfig { get; init; } = string.Empty;
    public Dictionary<string, string> Extras { get; } = new();

    public static DeviceHelloInfo? TryParse(string json)
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

            var info = new DeviceHelloInfo
            {
                FirmwareVersion = ReadString(doc.RootElement, "fw_version"),
                Board = ReadString(doc.RootElement, "board"),
                WifiStatus = ReadString(doc.RootElement, "wifi"),
                LastError = ReadString(doc.RootElement, "last_error"),
                CanReceiveConfig = ReadString(doc.RootElement, "can_config")
            };

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (info.Extras.ContainsKey(prop.Name))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    info.Extras[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
                else
                {
                    info.Extras[prop.Name] = prop.Value.ToString();
                }
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    public string ToSummary()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(FirmwareVersion)) parts.Add($"FW={FirmwareVersion}");
        if (!string.IsNullOrWhiteSpace(Board)) parts.Add($"Board={Board}");
        if (!string.IsNullOrWhiteSpace(WifiStatus)) parts.Add($"WiFi={WifiStatus}");
        if (!string.IsNullOrWhiteSpace(LastError)) parts.Add($"LastErr={LastError}");
        if (!string.IsNullOrWhiteSpace(CanReceiveConfig)) parts.Add($"Config={CanReceiveConfig}");
        return parts.Count == 0 ? "情報なし" : string.Join(" / ", parts);
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var prop) ? prop.ToString() : string.Empty;
    }
}
