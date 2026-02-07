using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class SerialService
{
    public int BaudRate { get; set; } = 115200;

    public Task<IReadOnlyList<SerialPortInfo>> DetectPortsAsync()
    {
        return Task.Run(() =>
        {
            var portNames = SerialPort.GetPortNames();
            var descriptions = GetPortDescriptions();
            var list = new List<SerialPortInfo>();

            foreach (var port in portNames)
            {
                descriptions.TryGetValue(port, out var desc);
                var info = new SerialPortInfo
                {
                    PortName = port,
                    Description = desc ?? string.Empty,
                    Score = ScorePort(desc)
                };
                list.Add(info);
            }

            return (IReadOnlyList<SerialPortInfo>)list
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.PortName)
                .ToList();
        });
    }

    public SerialPortInfo? SelectBestPort(IEnumerable<SerialPortInfo> ports)
    {
        return ports.OrderByDescending(p => p.Score).FirstOrDefault();
    }

    public async Task<HelloResult> HelloAsync(string portName)
    {
        var response = await SendCommandAsync(portName, "HELLO", TimeSpan.FromSeconds(5));
        if (response == null)
        {
            return new HelloResult { Success = false, Message = "デバイス応答がありません" };
        }

        if (!response.StartsWith("HELLO_OK", StringComparison.OrdinalIgnoreCase))
        {
            return new HelloResult { Success = false, Message = "応答が期待形式ではありません" };
        }

        var json = response["HELLO_OK".Length..].Trim();
        return new HelloResult { Success = true, Message = "OK", RawJson = json };
    }

    public async Task<ConfigResult> SendConfigAsync(string portName, DeviceConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        var response = await SendCommandAsync(portName, $"CFG_SET {json}", TimeSpan.FromSeconds(5));
        if (response == null)
        {
            return new ConfigResult { Success = false, Message = "設定送信がタイムアウトしました" };
        }

        if (!response.StartsWith("CFG_RESULT", StringComparison.OrdinalIgnoreCase))
        {
            return new ConfigResult { Success = false, Message = "設定結果が不明です" };
        }

        return ParseOkResult(response, "CFG_RESULT");
    }

    public async Task<ConfigResult> ApplyConfigAsync(string portName)
    {
        var response = await SendCommandAsync(portName, "CFG_APPLY", TimeSpan.FromSeconds(10));
        if (response == null)
        {
            return new ConfigResult { Success = false, Message = "適用がタイムアウトしました" };
        }

        if (!response.StartsWith("CFG_RESULT", StringComparison.OrdinalIgnoreCase))
        {
            return new ConfigResult { Success = false, Message = "適用結果が不明です" };
        }

        return ParseOkResult(response, "CFG_RESULT");
    }

    public async Task<DeviceTestResult> RunTestAsync(string portName)
    {
        var response = await SendCommandAsync(portName, "TEST_RUN", TimeSpan.FromSeconds(30));
        if (response == null)
        {
            return new DeviceTestResult { Success = false, Skipped = true, Message = "デバイス側テスト未実装の可能性" };
        }

        if (!response.StartsWith("TEST_RESULT", StringComparison.OrdinalIgnoreCase))
        {
            return new DeviceTestResult { Success = false, Message = "テスト結果が不明です" };
        }

        return ParseTestResult(response);
    }

    public async Task<string> DumpLogAsync(string portName)
    {
        try
        {
            using var serial = new SerialPort(portName, BaudRate)
            {
                NewLine = "\n",
                Encoding = Encoding.UTF8,
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };

            serial.Open();
            await using var writer = new StreamWriter(serial.BaseStream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(serial.BaseStream, Encoding.UTF8);

            await writer.WriteLineAsync("LOG_DUMP");

            var sb = new StringBuilder();
            var lastRead = DateTime.UtcNow;
            var hardLimit = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < hardLimit)
            {
                var line = await ReadLineAsync(reader, TimeSpan.FromMilliseconds(500));
                if (line == null)
                {
                    if (DateTime.UtcNow - lastRead > TimeSpan.FromSeconds(1))
                    {
                        break;
                    }

                    continue;
                }

                lastRead = DateTime.UtcNow;
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(line);
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LOG_DUMP failed");
            return string.Empty;
        }
    }

    private async Task<string?> SendCommandAsync(string portName, string command, TimeSpan timeout)
    {
        try
        {
            using var serial = new SerialPort(portName, BaudRate)
            {
                NewLine = "\n",
                Encoding = Encoding.UTF8,
                ReadTimeout = (int)timeout.TotalMilliseconds,
                WriteTimeout = (int)timeout.TotalMilliseconds
            };

            serial.Open();
            await using var writer = new StreamWriter(serial.BaseStream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(serial.BaseStream, Encoding.UTF8);

            Log.Information("Serial send {Command}", command.Split(' ')[0]);
            await writer.WriteLineAsync(command);

            var line = await ReadLineAsync(reader, timeout);
            if (line == null)
            {
                Log.Warning("Serial timeout for {Command}", command.Split(' ')[0]);
                return null;
            }

            Log.Information("Serial recv: {Line}", line);
            return line.Trim();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Serial command failed");
            return null;
        }
    }

    private async Task<string?> ReadLineAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await reader.ReadLineAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> GetPortDescriptions()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? string.Empty;
                var match = Regex.Match(name, "\\((COM\\d+)\\)");
                if (match.Success)
                {
                    map[match.Groups[1].Value] = name.Replace(match.Value, string.Empty).Trim();
                }
            }
        }
        catch
        {
            // WMI not available; ignore.
        }

        return map;
    }

    private static int ScorePort(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        var desc = description.ToLowerInvariant();
        var score = 0;
        if (desc.Contains("cp210")) score += 5;
        if (desc.Contains("silicon labs")) score += 4;
        if (desc.Contains("usb-serial")) score += 3;
        if (desc.Contains("ch340")) score += 2;
        return score;
    }

    private static ConfigResult ParseOkResult(string response, string prefix)
    {
        var json = response[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConfigResult { Success = false, Message = "結果JSONが空です" };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
            {
                return new ConfigResult { Success = true, Message = "OK" };
            }

            var reason = doc.RootElement.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() ?? "unknown"
                : "unknown";
            return new ConfigResult { Success = false, Message = reason };
        }
        catch
        {
            return new ConfigResult { Success = false, Message = "結果JSONが解析できません" };
        }
    }

    private static DeviceTestResult ParseTestResult(string response)
    {
        var json = response["TEST_RESULT".Length..].Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
            {
                return new DeviceTestResult { Success = true, Message = "OK" };
            }

            var stage = doc.RootElement.TryGetProperty("stage", out var stageProp)
                ? stageProp.GetString() ?? "unknown"
                : "unknown";
            var reason = doc.RootElement.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() ?? "unknown"
                : "unknown";
            return new DeviceTestResult { Success = false, Message = $"{stage}:{reason}" };
        }
        catch
        {
            return new DeviceTestResult { Success = false, Message = "結果JSONが解析できません" };
        }
    }
}
