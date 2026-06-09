using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VibeTracker.App.Views;

public class SelectedBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
            return new SolidColorBrush(Color.FromRgb(0xFB, 0xFD, 0xFE));
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SelectedFgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
            return new SolidColorBrush(Color.FromRgb(0x1E, 0x26, 0x2E));
        return new SolidColorBrush(Color.FromRgb(0x67, 0x76, 0x82));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
