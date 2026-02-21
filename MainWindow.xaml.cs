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
    private bool _isSyncingSensitiveInput;

    public MainWindow()
        : this(new MainViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        SyncSensitiveInputsFromViewModel();
        AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel), true);
    }

    private void WifiPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetSecretFromPasswordBox(box, value => _viewModel.ConfigWifiPassword = value, () => _viewModel.ConfigWifiPassword);
        }
    }

    private void OpenAiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetSecretFromPasswordBox(box, value => _viewModel.ConfigOpenAiKey = value, () => _viewModel.ConfigOpenAiKey);
        }
    }

    private void DucoMinerKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetSecretFromPasswordBox(box, value => _viewModel.DucoMinerKey = value, () => _viewModel.DucoMinerKey);
        }
    }

    private void AzureKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetSecretFromPasswordBox(box, value => _viewModel.AzureKey = value, () => _viewModel.AzureKey);
        }
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.PrepareForShutdown();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (source == null)
        {
            return;
        }

        // Keep dropdown list scrolling behavior intact while a ComboBox popup is open.
        if (IsInsideOpenComboBox(source))
        {
            return;
        }

        var scrollViewer = FindScrollableAncestor(source);

        // Let multiline text input keep its own wheel behavior if it can scroll itself.
        if (source is TextBox tb && tb.AcceptsReturn &&
            scrollViewer != null &&
            !ReferenceEquals(scrollViewer, MainStepScrollViewer))
        {
            return;
        }

        if (scrollViewer == null)
        {
            scrollViewer = MainStepScrollViewer;
        }

        if (scrollViewer == null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollableAncestor(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                return sv;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsInsideOpenComboBox(DependencyObject source)
    {
        var combo = FindAncestor<ComboBox>(source);
        return combo != null && combo.IsDropDownOpen;
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

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ConfigWifiPassword))
        {
            SyncPasswordBox(WifiPasswordBox, _viewModel.ConfigWifiPassword);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.DucoMinerKey))
        {
            SyncPasswordBox(DucoMinerKeyBox, _viewModel.DucoMinerKey);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.AzureKey))
        {
            SyncPasswordBox(AzureKeyBox, _viewModel.AzureKey);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.ConfigOpenAiKey))
        {
            SyncPasswordBox(OpenAiKeyBox, _viewModel.ConfigOpenAiKey);
        }
    }

    private void SetSecretFromPasswordBox(PasswordBox box, Action<string> setValue, Func<string> getValue)
    {
        if (_isSyncingSensitiveInput)
        {
            return;
        }

        setValue(box.Password);
        SyncPasswordBox(box, getValue());
    }

    private void SyncSensitiveInputsFromViewModel()
    {
        SyncPasswordBox(WifiPasswordBox, _viewModel.ConfigWifiPassword);
        SyncPasswordBox(DucoMinerKeyBox, _viewModel.DucoMinerKey);
        SyncPasswordBox(AzureKeyBox, _viewModel.AzureKey);
        SyncPasswordBox(OpenAiKeyBox, _viewModel.ConfigOpenAiKey);
    }

    private void SyncPasswordBox(PasswordBox box, string value)
    {
        var sanitized = value ?? string.Empty;
        if (string.Equals(box.Password, sanitized, StringComparison.Ordinal))
        {
            return;
        }

        _isSyncingSensitiveInput = true;
        try
        {
            box.Password = sanitized;
        }
        finally
        {
            _isSyncingSensitiveInput = false;
        }
    }
}
