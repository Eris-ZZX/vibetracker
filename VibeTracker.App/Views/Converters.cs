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
            return new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
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
            return new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB));
        return new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
