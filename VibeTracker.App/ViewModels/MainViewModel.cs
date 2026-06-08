using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VibeTracker.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private ProjectCardViewModel? _selectedProject;
    private DashboardViewModel? _dashboard;

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();

    public ProjectCardViewModel? SelectedProject
    {
        get => _selectedProject;
        set { _selectedProject = value; OnPropertyChanged(); }
    }

    public DashboardViewModel? Dashboard
    {
        get => _dashboard;
        set { _dashboard = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
