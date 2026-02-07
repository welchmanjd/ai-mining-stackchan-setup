using System.Windows;
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
}
