using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public partial class SerialService
{
    public Task<string> CapturePostRebootLogAsync(string portName, TimeSpan duration, CancellationToken token)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            var deadline = DateTime.UtcNow + duration;
            SerialPort? serial = null;

            try
            {
                // Ensure command channel is released before monitor capture.
                Close();

                while (DateTime.UtcNow < deadline)
                {
                    token.ThrowIfCancellationRequested();

                    if (serial == null || !serial.IsOpen)
                    {
                        try
                        {
                            serial = new SerialPort(portName, BaudRate)
                            {
                                NewLine = "\n",
                                Encoding = Utf8NoBom,
                                ReadTimeout = 300,
                                WriteTimeout = 1000,
                                Handshake = Handshake.None,
                                DtrEnable = false,
                                RtsEnable = false
                            };
                            serial.Open();
                        }
                        catch
                        {
                            CloseLockedPort(serial);
                            serial = null;
                            Thread.Sleep(200);
                            continue;
                        }
                    }

                    try
                    {
                        var line = serial.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        sb.Append('[')
                          .Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                          .Append("] ")
                          .AppendLine(line.TrimEnd('\r', '\n'));
                    }
                    catch (TimeoutException)
                    {
                        // keep waiting until deadline
                    }
                    catch
                    {
                        CloseLockedPort(serial);
                        serial = null;
                    }
                }
            }
            finally
            {
                CloseLockedPort(serial);
                Close();
            }

            return sb.ToString();
        }, token);
    }

    public Task<DeviceTestResult> RunTestAsync(string portName)
    {
        return RunTestAsync(portName, CancellationToken.None);
    }

    public Task<DeviceTestResult> RunTestAsync(string portName, CancellationToken token)
    {
        return Task.FromResult(new DeviceTestResult { Success = false, Skipped = true, Message = "デバイステスト機能は現在利用できません" });
    }

    public Task<DeviceInfo?> ReadBootBannerInfoAsync(string portName, TimeSpan timeout, CancellationToken token)
    {
        SerialPort? serial = null;
        try
        {
            Close();

            serial = new SerialPort(portName, BaudRate)
            {
                NewLine = "\n",
                Encoding = Utf8NoBom,
                ReadTimeout = 250,
                WriteTimeout = 250,
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false
            };
            serial.Open();
            try { serial.DiscardInBuffer(); } catch { }

            var deadline = DateTime.UtcNow + timeout;
            var regex = new Regex(@"Mining-Stackchan-Core2\s+([0-9]+(?:\.[0-9]+){1,3})", RegexOptions.IgnoreCase);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                string? line = null;
                try
                {
                    line = serial.ReadLine();
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var m = regex.Match(line);
                if (!m.Success)
                {
                    continue;
                }

                return Task.FromResult<DeviceInfo?>(new DeviceInfo
                {
                    App = "Mining-Stackchan-Core2",
                    Ver = m.Groups[1].Value,
                    BuildId = "boot-banner",
                    Baud = BaudRate
                });
            }
        }
        catch
        {
            // best effort
        }
        finally
        {
            CloseLockedPort(serial);
        }

        return Task.FromResult<DeviceInfo?>(null);
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

    private string DumpLogCore(string portName, CancellationToken token)
    {
        var sb = new StringBuilder();
        try
        {
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
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        Handshake = Handshake.None,
                        DtrEnable = false,
                        RtsEnable = false
                    };
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
            Log.Warning(ex, "serial.log_dump.aborted_port_closed");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "serial.log_dump.failed");
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
}
