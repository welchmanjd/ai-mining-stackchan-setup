using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AiStackchanSetup.Services;

public partial class SerialService
{
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

            Log.Information("Serial send {Command}", command.Split(' ')[0]);
            trace.AppendLine("write: ok");
            serial.WriteLine(command);

            var line = await ReadResponseLineAsync(serial, timeout, trace, token);
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
                    serial.WriteLine(command);
                    line = await ReadResponseLineAsync(serial, TimeSpan.FromSeconds(3), trace, token);
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
        catch (TimeoutException ex)
        {
            // Timeout is often transient during boot logs; keep port open to avoid
            // triggering extra resets by close/open cycles on next probe.
            Log.Warning(ex, "Serial command timeout");
            trace.AppendLine($"error: {ex.GetType().Name}: {ex.Message}");
            await AppendSerialTraceAsync(trace);
            throw;
        }
        catch (SerialCommandException ex)
        {
            // Protocol-level error; keep port open for subsequent retry/commands.
            Log.Warning(ex, "Serial command protocol error");
            trace.AppendLine($"error: {ex.GetType().Name}: {ex.Message}");
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

    private async Task<string?> ReadLineAsync(SerialPort serial, TimeSpan timeout, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + timeout;
        var originalReadTimeout = serial.ReadTimeout;
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                var remainingMs = (int)Math.Clamp((deadline - DateTime.UtcNow).TotalMilliseconds, 1, 300);
                serial.ReadTimeout = remainingMs;
                try
                {
                    return await Task.Run(serial.ReadLine, token);
                }
                catch (TimeoutException)
                {
                    // continue until deadline
                }
            }

            return null;
        }
        finally
        {
            serial.ReadTimeout = originalReadTimeout;
        }
    }

    private async Task<string?> ReadResponseLineAsync(SerialPort serial, TimeSpan timeout, StringBuilder trace, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            var line = await ReadLineAsync(serial, remaining, token);
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
}
