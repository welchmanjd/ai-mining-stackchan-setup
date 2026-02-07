namespace AiStackchanSetup.Models;

public class SerialPortInfo
{
    public string PortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Score { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Description)
        ? PortName
        : $"{PortName} - {Description}";
}
