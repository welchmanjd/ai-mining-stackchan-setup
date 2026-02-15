using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

public interface ISerialService
{
    string LastProtocolResponse { get; }
    string LastInfoJson { get; }

    Task<IReadOnlyList<SerialPortInfo>> DetectPortsAsync(CancellationToken token);
    SerialPortInfo? SelectBestPort(IEnumerable<SerialPortInfo> ports);
    Task<HelloResult> HelloAsync(string portName, CancellationToken token);
    Task<CommandResult> PingAsync(string portName, CancellationToken token);
    Task<DeviceInfoResult> GetInfoAsync(string portName, CancellationToken token);
    Task<DeviceInfoResult> GetInfoAsync(string portName, TimeSpan timeout, CancellationToken token);
    Task<DeviceInfo?> ReadBootBannerInfoAsync(string portName, TimeSpan timeout, CancellationToken token);
    Task<ConfigJsonResult> GetConfigJsonAsync(string portName, CancellationToken token);
    Task<ConfigResult> SendConfigAsync(string portName, DeviceConfig config, CancellationToken token);
    Task<ConfigResult> ApplyConfigAsync(string portName, CancellationToken token);
    Task<string> CapturePostRebootLogAsync(string portName, TimeSpan duration, CancellationToken token);
    Task<DeviceTestResult> RunTestAsync(string portName, CancellationToken token);
    Task<string> DumpLogAsync(string portName);
    void Close();
}

public interface IFlashService
{
    Task<FlashResult> FlashAsync(string portName, int baud, bool erase, string firmwarePath, CancellationToken token);
    void KillActiveProcesses();
}

public interface IApiTestService
{
    Task<ApiTestResult> TestAsync(string apiKey, CancellationToken token);
    Task<ApiTestResult> TestAzureSpeechAsync(string apiKey, string region, string customSubdomain, CancellationToken token);
}

public interface ISupportPackService
{
    Task<string> CreateSupportPackAsync(SupportSummary summary, DeviceConfig config);
}
