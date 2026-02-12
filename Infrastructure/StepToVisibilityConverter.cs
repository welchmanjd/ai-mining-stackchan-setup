using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AiStackchanSetup.Infrastructure;

public class StepToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int step && parameter != null && int.TryParse(parameter.ToString(), out var target))
        {
            return step == target ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
