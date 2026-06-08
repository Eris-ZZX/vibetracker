using System.Windows;

namespace VibeTracker.App.Views;

public partial class PathInputDialog : Window
{
    public string NewPath => TxtPath.Text.Trim();

    public PathInputDialog(string currentPath)
    {
        InitializeComponent();
        TxtPath.Text = currentPath;
        TxtPath.Focus();
        TxtPath.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
