namespace AiStackchanSetup.Models;

public class SupportSummary
{
    public string AppVersion { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AppBaseDirectory { get; set; } = string.Empty;
    public string FirmwarePath { get; set; } = string.Empty;
    public string FirmwareInfo { get; set; } = string.Empty;
    public string DetectedPorts { get; set; } = string.Empty;
    public string SelectedPort { get; set; } = string.Empty;
    public string FlashResult { get; set; } = string.Empty;
    public string ApiTest { get; set; } = string.Empty;
    public string DeviceTest { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public string DeviceInfoJson { get; set; } = string.Empty;
    public string LastProtocolResponse { get; set; } = string.Empty;
    public MaskedDeviceConfig? Config { get; set; }
}
