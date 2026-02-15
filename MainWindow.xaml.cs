using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel), true);
    }

    private void WifiPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box)
        {
            _viewModel.ConfigWifiPassword = box.Password;
        }
    }

    private void OpenAiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box)
        {
            _viewModel.ConfigOpenAiKey = box.Password;
        }
    }

    private void DucoMinerKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box)
        {
            _viewModel.DucoMinerKey = box.Password;
        }
    }

    private void AzureKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box)
        {
            _viewModel.AzureKey = box.Password;
        }
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.PrepareForShutdown();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (source == null)
        {
            return;
        }

        // Let multiline text input keep its own wheel behavior.
        if (source is TextBox tb && tb.AcceptsReturn)
        {
            return;
        }

        var scrollViewer = FindAncestor<ScrollViewer>(source);
        if (scrollViewer == null)
        {
            scrollViewer = MainStepScrollViewer;
        }

        if (scrollViewer == null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T found)
            {
                return found;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
