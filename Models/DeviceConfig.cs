namespace AiStackchanSetup.Models;

public class DeviceConfig
{
    public string WifiSsid { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public string DucoUser { get; set; } = string.Empty;
    public string DucoMinerKey { get; set; } = string.Empty;
    public string OpenAiKey { get; set; } = string.Empty;
    public string AzureKey { get; set; } = string.Empty;
    public string AzureRegion { get; set; } = string.Empty;
    public string AzureCustomSubdomain { get; set; } = string.Empty;

    public MaskedDeviceConfig ToMasked()
    {
        return new MaskedDeviceConfig
        {
            WifiSsid = WifiSsid,
            WifiPassword = Mask(WifiPassword),
            DucoUser = DucoUser,
            DucoMinerKey = Mask(DucoMinerKey),
            OpenAiKey = Mask(OpenAiKey),
            AzureKey = Mask(AzureKey),
            AzureRegion = AzureRegion,
            AzureCustomSubdomain = AzureCustomSubdomain
        };
    }

    public static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}

public class MaskedDeviceConfig
{
    public string WifiSsid { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public string DucoUser { get; set; } = string.Empty;
    public string DucoMinerKey { get; set; } = string.Empty;
    public string OpenAiKey { get; set; } = string.Empty;
    public string AzureKey { get; set; } = string.Empty;
    public string AzureRegion { get; set; } = string.Empty;
    public string AzureCustomSubdomain { get; set; } = string.Empty;
}
