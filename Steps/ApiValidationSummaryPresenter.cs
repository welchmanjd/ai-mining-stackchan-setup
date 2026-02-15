using System.Windows.Media;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

internal static class ApiValidationSummaryPresenter
{
    public static void SetOpenAiNeutral(MainViewModel vm, string message) =>
        Apply(vm, openAi: true, message, Color.FromRgb(0x6B, 0x72, 0x80), Color.FromRgb(0xF3, 0xF4, 0xF6));

    public static void SetOpenAiOk(MainViewModel vm, string message) =>
        Apply(vm, openAi: true, message, Color.FromRgb(0x16, 0xA3, 0x4A), Color.FromRgb(0xDC, 0xF7, 0xE3));

    public static void SetOpenAiNg(MainViewModel vm, string message) =>
        Apply(vm, openAi: true, message, Color.FromRgb(0xDC, 0x26, 0x26), Color.FromRgb(0xFE, 0xE2, 0xE2));

    public static void SetAzureNeutral(MainViewModel vm, string message) =>
        Apply(vm, openAi: false, message, Color.FromRgb(0x6B, 0x72, 0x80), Color.FromRgb(0xF3, 0xF4, 0xF6));

    public static void SetAzureOk(MainViewModel vm, string message) =>
        Apply(vm, openAi: false, message, Color.FromRgb(0x16, 0xA3, 0x4A), Color.FromRgb(0xDC, 0xF7, 0xE3));

    public static void SetAzureNg(MainViewModel vm, string message) =>
        Apply(vm, openAi: false, message, Color.FromRgb(0xDC, 0x26, 0x26), Color.FromRgb(0xFE, 0xE2, 0xE2));

    private static void Apply(MainViewModel vm, bool openAi, string message, Color brushColor, Color bgColor)
    {
        var brush = new SolidColorBrush(brushColor);
        var bg = new SolidColorBrush(bgColor);
        if (openAi)
        {
            vm.ApiTestSummary = message;
            vm.ApiTestSummaryBrush = brush;
            vm.ApiTestSummaryBackground = bg;
            return;
        }

        vm.AzureTestSummary = message;
        vm.AzureTestSummaryBrush = brush;
        vm.AzureTestSummaryBackground = bg;
    }
}
