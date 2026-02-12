using System;
using System.Collections.Generic;
using System.Globalization;
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

public class SerialService : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    public int BaudRate { get; set; } = 115200;
    private const string EmptyValueSentinel = "__MC_EMPTY__";

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
                    Score = ScorePort(desc)
                };
                list.Add(info);
            }

            return (IReadOnlyList<SerialPortInfo>)list
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.PortName)
                .ToList();
        }, token);
    }

    public SerialPortInfo? SelectBestPort(IEnumerable<SerialPortInfo> ports)
    {
        return ports.OrderByDescending(p => p.Score).FirstOrDefault();
    }

    public Task<HelloResult> HelloAsync(string portName)
    {
        return HelloAsync(portName, CancellationToken.None);
    }

    public async Task<HelloResult> HelloAsync(string portName, CancellationToken token)
    {
        try
        {
            var response = await SendCommandAsync(portName, "HELLO", TimeSpan.FromSeconds(8), token);
            if (response.StartsWith("@OK HELLO", StringComparison.OrdinalIgnoreCase))
            {
                return new HelloResult { Success = true, Message = "OK" };
            }

            return new HelloResult { Success = false, Message = "応答が期待形式ではありません" };
        }
        catch (SerialCommandException ex)
        {
            return new HelloResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new HelloResult { Success = false, Message = ex.Message };
        }
    }

    public Task<CommandResult> PingAsync(string portName)
    {
        return PingAsync(portName, CancellationToken.None);
    }

    public async Task<CommandResult> PingAsync(string portName, CancellationToken token)
    {
        try
        {
            var response = await SendCommandAsync(portName, "PING", TimeSpan.FromSeconds(8), token);
            if (response.StartsWith("@OK PONG", StringComparison.OrdinalIgnoreCase))
            {
                return new CommandResult { Success = true, Message = "OK" };
            }

            return new CommandResult { Success = false, Message = "応答が期待形式ではありません" };
        }
        catch (SerialCommandException ex)
        {
            return new CommandResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }

    public Task<DeviceInfoResult> GetInfoAsync(string portName)
    {
        return GetInfoAsync(portName, CancellationToken.None);
    }

    public async Task<DeviceInfoResult> GetInfoAsync(string portName, CancellationToken token)
    {
        return await GetInfoAsync(portName, TimeSpan.FromSeconds(8), token);
    }

    public async Task<DeviceInfoResult> GetInfoAsync(string portName, TimeSpan timeout, CancellationToken token)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await SendCommandAsync(portName, "GET INFO", timeout, token);
                if (!response.StartsWith("@INFO", StringComparison.OrdinalIgnoreCase))
                {
                    return new DeviceInfoResult { Success = false, Message = "応答が期待形式ではありません" };
                }

                var json = response["@INFO".Length..].Trim();
                var info = DeviceInfo.TryParse(json);
                if (info == null)
                {
                    return new DeviceInfoResult { Success = false, Message = "INFO JSONが解析できません" };
                }

                return new DeviceInfoResult { Success = true, Message = "OK", RawJson = json, Info = info };
            }
            catch (SerialCommandException ex) when (attempt == 0 && IsTransientInfoSyncNoise(ex.Reason))
            {
                await Task.Delay(180, token);
                continue;
            }
            catch (SerialCommandException ex)
            {
                return new DeviceInfoResult { Success = false, Message = ex.Reason };
            }
            catch (TimeoutException ex)
            {
                return new DeviceInfoResult { Success = false, Message = ex.Message };
            }
        }

        return new DeviceInfoResult { Success = false, Message = "GET INFO failed after retry" };
    }

    public Task<(bool Success, string Message, string Json)> GetConfigJsonAsync(string portName)
    {
        return GetConfigJsonAsync(portName, CancellationToken.None);
    }

    public async Task<(bool Success, string Message, string Json)> GetConfigJsonAsync(string portName, CancellationToken token)
    {
        try
        {
            var response = await SendCommandAsync(portName, "GET CFG", TimeSpan.FromSeconds(8), token);
            if (!response.StartsWith("@CFG", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "応答が期待形式ではありません", string.Empty);
            }

            var json = response["@CFG".Length..].Trim();
            return (true, "OK", json);
        }
        catch (SerialCommandException ex)
        {
            return (false, ex.Reason, string.Empty);
        }
        catch (TimeoutException ex)
        {
            return (false, ex.Message, string.Empty);
        }
    }

    public Task<ConfigResult> SendConfigAsync(string portName, DeviceConfig config)
    {
        return SendConfigAsync(portName, config, CancellationToken.None);
    }

    public async Task<ConfigResult> SendConfigAsync(string portName, DeviceConfig config, CancellationToken token)
    {
        var warnings = new List<string>();
        async Task<ConfigResult> SendSetWithCompatAsync(string key, string value, bool allowUnknownKey)
        {
            var result = await SendSetAsync(portName, key, value, token);
            if (result.Success)
            {
                return result;
            }

            if (allowUnknownKey && result.Message.Contains("unknown_key", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{key}:unsupported");
                return new ConfigResult { Success = true, Message = "SKIP" };
            }

            return result;
        }

        {
            var result = await SendSetWithCompatAsync("wifi_enabled", config.WifiEnabled ? "1" : "0", allowUnknownKey: true);
            if (!result.Success) return result;
        }
        {
            var result = await SendSetWithCompatAsync("mining_enabled", config.MiningEnabled ? "1" : "0", allowUnknownKey: true);
            if (!result.Success) return result;
        }
        {
            var result = await SendSetWithCompatAsync("ai_enabled", config.AiEnabled ? "1" : "0", allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("wifi_ssid", config.WifiSsid, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.WifiPassword))
        {
            var result = await SendSetWithCompatAsync("wifi_pass", config.WifiPassword, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("duco_user", config.DucoUser, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.DucoMinerKey))
        {
            var result = await SendSetWithCompatAsync("duco_miner_key", config.DucoMinerKey, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("az_speech_region", config.AzureRegion, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.AzureKey))
        {
            var result = await SendSetWithCompatAsync("az_speech_key", config.AzureKey, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("az_custom_subdomain", config.AzureCustomSubdomain, allowUnknownKey: false);
            if (!result.Success) return result;
        }

        if (!string.IsNullOrWhiteSpace(config.OpenAiKey))
        {
            var result = await SendSetWithCompatAsync("openai_key", config.OpenAiKey, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("openai_model", config.OpenAiModel, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("openai_instructions", config.OpenAiInstructions, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("display_sleep_s", config.DisplaySleepSeconds.ToString(CultureInfo.InvariantCulture), allowUnknownKey: true);
            if (!result.Success) return result;
        }
        {
            var result = await SendSetWithCompatAsync("spk_volume", config.SpeakerVolume.ToString(CultureInfo.InvariantCulture), allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("share_accepted_text", config.ShareAcceptedText, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("attention_text", config.AttentionText, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        {
            var result = await SendSetWithCompatAsync("hello_text", config.HelloText, allowUnknownKey: true);
            if (!result.Success) return result;
        }

        return new ConfigResult
        {
            Success = warnings.Count == 0,
            Message = warnings.Count > 0 ? $"一部キー未対応: {string.Join(", ", warnings)}" : "OK"
        };
    }

    public Task<ConfigResult> ApplyConfigAsync(string portName)
    {
        return ApplyConfigAsync(portName, CancellationToken.None);
    }

    public async Task<ConfigResult> ApplyConfigAsync(string portName, CancellationToken token)
    {
        try
        {
            var saveResponse = await SendCommandAsync(portName, "SAVE", TimeSpan.FromSeconds(10), token);
            if (!saveResponse.StartsWith("@OK SAVE", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigResult { Success = false, Message = "保存結果が不明です" };
            }

            try
            {
                var rebootResponse = await SendCommandAsync(portName, "REBOOT", TimeSpan.FromSeconds(10), token);
                if (!rebootResponse.StartsWith("@OK REBOOT", StringComparison.OrdinalIgnoreCase))
                {
                    return new ConfigResult { Success = false, Message = "再起動結果が不明です" };
                }
            }
            catch (TimeoutException)
            {
                return new ConfigResult { Success = true, Message = "OK (rebooting)" };
            }
            catch (IOException)
            {
                return new ConfigResult { Success = true, Message = "OK (rebooting)" };
            }

            return new ConfigResult { Success = true, Message = "OK" };
        }
        catch (SerialCommandException ex)
        {
            return new ConfigResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    public Task<DeviceTestResult> RunTestAsync(string portName)
    {
        return RunTestAsync(portName, CancellationToken.None);
    }

    public Task<DeviceTestResult> RunTestAsync(string portName, CancellationToken token)
    {
        return Task.FromResult(new DeviceTestResult { Success = false, Skipped = true, Message = "デバイス側テスト未実装の可能性" });
    }

    public string LastProtocolResponse { get; private set; } = string.Empty;
    public string LastInfoJson { get; private set; } = string.Empty;

    public Task<string> DumpLogAsync(string portName)
    {
        return DumpLogAsync(portName, CancellationToken.None);
    }

    public Task<string> DumpLogAsync(string portName, CancellationToken token)
    {
        return Task.Run(() => DumpLogCore(portName, token), token);
    }

    private SerialPort? _activePort;
    private readonly object _portLock = new();

    public void Close()
    {
        lock (_portLock)
        {
            if (_activePort != null)
            {
                try
                {
                    if (_activePort.IsOpen) _activePort.Close();
                }
                catch { /* ignore */ }
                _activePort.Dispose();
                _activePort = null;
            }
        }
    }

    private void CloseLockedPort(SerialPort? port)
    {
        if (port == null)
        {
            return;
        }

        try
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            port.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        Close();
    }

    private async Task<string> SendCommandAsync(string portName, string command, TimeSpan timeout, CancellationToken token)
    {
        var trace = new StringBuilder();
        trace.AppendLine("=== serial command ===");
        trace.AppendLine($"timestamp: {DateTimeOffset.Now:O}");
        trace.AppendLine($"port: {portName}");
        trace.AppendLine($"baud: {BaudRate}");
        trace.AppendLine($"timeout_ms: {(int)timeout.TotalMilliseconds}");
        trace.AppendLine($"command: {RedactCommand(command)}");

        SerialPort serial;
        SerialPort? portToClose = null;
        bool createdNew = false;
        
        lock (_portLock)
        {
            if (_activePort != null && _activePort.PortName != portName)
            {
                portToClose = _activePort;
                _activePort = null;
            }

            if (_activePort == null)
            {
                _activePort = new SerialPort(portName, BaudRate)
                {
                    NewLine = "\n",
                    Encoding = Utf8NoBom,
                    ReadTimeout = (int)timeout.TotalMilliseconds,
                    WriteTimeout = (int)timeout.TotalMilliseconds,
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false
                };
                createdNew = true;
            }
            serial = _activePort;
        }
        CloseLockedPort(portToClose);

        try
        {
            if (!serial.IsOpen)
            {
                serial.Open();
                // When freshly opened, wait a bit and clear garbage
                try { serial.DiscardInBuffer(); } catch { /* ignore */ }
                try { serial.DiscardOutBuffer(); } catch { /* ignore */ }
                await Task.Delay(150, token);
            }
            
            // Adjust timeouts for this specific command
            serial.ReadTimeout = (int)timeout.TotalMilliseconds;
            serial.WriteTimeout = (int)timeout.TotalMilliseconds;

            // Do NOT utilize 'using' as we want to keep it open
            // Do NOT re-create StreamWriter/Reader every time if possible, but for safety in this refactoring 
            // we will create wrappers around the BaseStream. 
            // Note: Closing StreamWriter/Reader closes the BaseStream, so we must be careful.
            // Argument 'leaveOpen' is available in recent .NET versions or manually handle stream.
            
            // For simple refactoring without breaking 'using' mechanics on readers, 
            // we will use the SerialPort direct methods or non-closing wrappers.
            // Actually, SerialPort.WriteLine / ReadLine are available but synchronous.
            // We need Async.
            
            // Allow stream wrappers to NOT close the underlying stream
            using var streamWrapper = new NonClosingStreamWrapper(serial.BaseStream);
            using var writer = new StreamWriter(streamWrapper, Utf8NoBom) { AutoFlush = true };
            using var reader = new StreamReader(streamWrapper, Utf8NoBom);

            Log.Information("Serial send {Command}", command.Split(' ')[0]);
            trace.AppendLine("write: ok");
            await writer.WriteLineAsync(command);

            var line = await ReadResponseLineAsync(reader, timeout, trace, token);
            if (line == null)
            {
                // Check for boot messages if we just connected or if device reset
                var bootDetected = trace.ToString().Contains("boot:", StringComparison.OrdinalIgnoreCase)
                                   || trace.ToString().Contains("entry 0x", StringComparison.OrdinalIgnoreCase)
                                   || trace.ToString().Contains("[MAIN] setup() start", StringComparison.OrdinalIgnoreCase)
                                   || trace.ToString().Contains("ets Jul", StringComparison.OrdinalIgnoreCase);
                                   
                if (bootDetected && !command.StartsWith("REBOOT", StringComparison.OrdinalIgnoreCase))
                {
                    trace.AppendLine("retry: boot_detected_resend");
                    await Task.Delay(250, token);
                    await writer.WriteLineAsync(command);
                    line = await ReadResponseLineAsync(reader, TimeSpan.FromSeconds(3), trace, token);
                }
            }

            if (line == null)
            {
                Log.Warning("Serial timeout for {Command}", command.Split(' ')[0]);
                trace.AppendLine("read: timeout");
                await AppendSerialTraceAsync(trace);
                throw new TimeoutException($"デバイス応答がタイムアウトしました ({command})");
            }

            Log.Information("Serial recv: {Line}", line);
            trace.AppendLine($"read: {line}");
            await AppendSerialTraceAsync(trace);
            var trimmed = line.Trim();
            
            // Update state
            if (trimmed.StartsWith("@INFO", StringComparison.OrdinalIgnoreCase))
            {
                LastInfoJson = trimmed["@INFO".Length..].Trim();
            }
            LastProtocolResponse = trimmed;

            if (trimmed.StartsWith("@ERR", StringComparison.OrdinalIgnoreCase))
            {
                var reason = trimmed["@ERR".Length..].Trim();
                throw new SerialCommandException(reason, trimmed);
            }

            return trimmed;
        }
        catch (OperationCanceledException)
        {
            trace.AppendLine("error: cancelled");
            await AppendSerialTraceAsync(trace);
            throw;
        }
        catch (Exception ex)
        {
            // On error, force close to ensure fresh state next time
            Log.Error(ex, "Serial command failed");
            trace.AppendLine($"error: {ex.GetType().Name}: {ex.Message}");
            await AppendSerialTraceAsync(trace);
            Close(); 
            throw;
        }
    }

    private string DumpLogCore(string portName, CancellationToken token)
    {
        var sb = new StringBuilder();
        try
        {
            SerialPort serial;
            SerialPort? portToClose = null;
            bool createdNew = false;

            lock (_portLock)
            {
                if (_activePort != null && _activePort.PortName != portName)
                {
                    portToClose = _activePort;
                    _activePort = null;
                }

                if (_activePort == null)
                {
                    _activePort = new SerialPort(portName, BaudRate)
                    {
                        NewLine = "\n",
                        Encoding = Utf8NoBom,
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        Handshake = Handshake.None,
                        DtrEnable = false,
                        RtsEnable = false
                    };
                    createdNew = true;
                }
                serial = _activePort;
            }
            CloseLockedPort(portToClose);

            if (!serial.IsOpen)
            {
                serial.Open();
                try { serial.DiscardInBuffer(); } catch { /* ignore */ }
                try { serial.DiscardOutBuffer(); } catch { /* ignore */ }
            }

            serial.ReadTimeout = 2000;
            serial.WriteTimeout = 2000;

            using var streamWrapper = new NonClosingStreamWrapper(serial.BaseStream);
            using var writer = new StreamWriter(streamWrapper, Utf8NoBom) { AutoFlush = true };
            using var reader = new StreamReader(streamWrapper, Utf8NoBom);

            writer.WriteLine("LOG_DUMP");

            var lastRead = DateTime.UtcNow;
            var hardLimit = DateTime.UtcNow.AddSeconds(10);
            var originalReadTimeout = serial.ReadTimeout;
            serial.ReadTimeout = 500;

            try
            {
                while (DateTime.UtcNow < hardLimit)
                {
                    token.ThrowIfCancellationRequested();
                    string? line = null;
                    try
                    {
                        line = reader.ReadLine();
                    }
                    catch (TimeoutException)
                    {
                        line = null;
                    }
                    if (line == null)
                    {
                        if (DateTime.UtcNow - lastRead > TimeSpan.FromSeconds(1))
                        {
                            break;
                        }

                        continue;
                    }

                    lastRead = DateTime.UtcNow;
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("@ERR", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SerialCommandException(trimmed["@ERR".Length..].Trim(), trimmed);
                    }
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }

                    sb.Append(line);
                }
            }
            finally
            {
                serial.ReadTimeout = originalReadTimeout;
            }

            return sb.ToString();
        }
        catch (SerialCommandException)
        {
            throw;
        }
        catch (ObjectDisposedException ex)
        {
            Log.Warning(ex, "LOG_DUMP aborted (port closed)");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LOG_DUMP failed");
            return string.Empty;
        }
    }
    
    // Helper class to prevent StreamWriter/Reader from closing the SerialPort
    private class NonClosingStreamWrapper : Stream
    {
        private readonly Stream _base;
        public NonClosingStreamWrapper(Stream baseStream) => _base = baseStream;
        public override void Close() { /* do nothing */ }
        protected override void Dispose(bool disposing) { /* do nothing */ }
        public override bool CanRead => _base.CanRead;
        public override bool CanSeek => _base.CanSeek;
        public override bool CanWrite => _base.CanWrite;
        public override long Length => _base.Length;
        public override long Position { get => _base.Position; set => _base.Position = value; }
        public override void Flush() => _base.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _base.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _base.Seek(offset, origin);
        public override void SetLength(long value) => _base.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _base.Write(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _base.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _base.ReadAsync(buffer, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _base.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _base.WriteAsync(buffer, cancellationToken);
        public override Task FlushAsync(CancellationToken cancellationToken) => _base.FlushAsync(cancellationToken);
    }

    private async Task<string?> ReadLineAsync(StreamReader reader, TimeSpan timeout, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(timeout);
        try
        {
            return await reader.ReadLineAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                throw;
            }
            return null;
        }
    }

    private async Task<string?> ReadResponseLineAsync(StreamReader reader, TimeSpan timeout, StringBuilder trace, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            var line = await ReadLineAsync(reader, remaining, token);
            if (line == null)
            {
                return null;
            }

            // Ignore non-protocol log lines from the device.
            if (!line.StartsWith("@", StringComparison.OrdinalIgnoreCase))
            {
                trace.AppendLine($"read: {line}");
                trace.AppendLine("read: ignored (non-protocol)");
                continue;
            }

            if (!(line.StartsWith("@OK", StringComparison.OrdinalIgnoreCase) ||
                  line.StartsWith("@INFO", StringComparison.OrdinalIgnoreCase) ||
                  line.StartsWith("@CFG", StringComparison.OrdinalIgnoreCase) ||
                  line.StartsWith("@ERR", StringComparison.OrdinalIgnoreCase)))
            {
                trace.AppendLine($"read: {line}");
                trace.AppendLine("read: ignored (unknown protocol)");
                continue;
            }

            return line;
        }

        return null;
    }

    private static async Task AppendSerialTraceAsync(StringBuilder trace)
    {
        try
        {
            Directory.CreateDirectory(LogService.LogDirectory);
            await File.AppendAllTextAsync(LogService.SerialLogPath, trace.ToString() + Environment.NewLine);
        }
        catch
        {
            // ignore logging errors
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

    private async Task<ConfigResult> SendSetAsync(string portName, string key, string value, CancellationToken token)
    {
        try
        {
            var encodedValue = string.IsNullOrEmpty(value) ? EmptyValueSentinel : value;
            var response = await SendCommandAsync(portName, $"SET {key} {encodedValue}", TimeSpan.FromSeconds(8), token);
            if (response.StartsWith("@OK SET", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigResult { Success = true, Message = "OK" };
            }

            return new ConfigResult { Success = false, Message = $"設定結果が不明です ({key})" };
        }
        catch (SerialCommandException ex)
        {
            return new ConfigResult { Success = false, Message = $"設定失敗({key}): {ex.Reason}" };
        }
        catch (TimeoutException ex)
        {
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    private static string RedactCommand(string command)
    {
        if (!command.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
        {
            return command;
        }

        var parts = command.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return command;
        }

        var key = parts[1];
        if (IsSensitiveKey(key))
        {
            return $"{parts[0]} {parts[1]} {DeviceConfig.Mask(parts[2])}";
        }

        return command;
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Equals("wifi_pass", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("duco_miner_key", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("az_speech_key", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("openai_key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientInfoSyncNoise(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        if (!reason.StartsWith("unknown_cmd:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = reason["unknown_cmd:".Length..].Trim();
        return payload.Length > 0 && payload.All(ch => ch == 'U');
    }

}
