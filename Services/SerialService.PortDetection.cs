using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

public partial class SerialService
{
    public Task<IReadOnlyList<SerialPortInfo>> DetectPortsAsync()
    {
        return DetectPortsAsync(CancellationToken.None);
    }

    public Task<IReadOnlyList<SerialPortInfo>> DetectPortsAsync(CancellationToken token)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
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
                    Score = ScorePort(port, desc)
                };
                list.Add(info);
            }

            return (IReadOnlyList<SerialPortInfo>)list
                .OrderByDescending(p => p.Score)
                .ThenByDescending(p => ParseComNumber(p.PortName))
                .ThenBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, token);
    }

    public SerialPortInfo? SelectBestPort(IEnumerable<SerialPortInfo> ports)
    {
        return ports
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => ParseComNumber(p.PortName))
            .ThenBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
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

    private static int ScorePort(string portName, string? description)
    {
        var score = 0;
        var comNo = ParseComNumber(portName);

        // Prefer typical USB serial adapters over legacy motherboard ports.
        if (comNo >= 3) score += 2;
        if (comNo >= 5) score += 1;
        if (comNo == 1) score -= 4;

        if (!string.IsNullOrWhiteSpace(description))
        {
            var desc = description.ToLowerInvariant();
            if (desc.Contains("cp210")) score += 7;
            if (desc.Contains("silicon labs")) score += 6;
            if (desc.Contains("usb serial")) score += 5;
            if (desc.Contains("usb-serial")) score += 5;
            if (desc.Contains("ch340")) score += 5;
            if (desc.Contains("ch910")) score += 5;
            if (desc.Contains("ftdi")) score += 5;
            if (desc.Contains("m5stack")) score += 6;
            if (desc.Contains("bluetooth")) score -= 5;
            if (desc.Contains("communications port")) score -= 3;
            if (desc.Contains("standard serial")) score -= 2;
        }

        return score;
    }

    private static int ParseComNumber(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return 0;
        }

        if (!portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(portName[3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
