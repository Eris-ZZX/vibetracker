using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VibeTracker.App.ViewModels;

public class ProjectCardViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Tag { get; set; } = "轻量应用";

    private string _status = "in_progress";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); }
    }

    public string StatusIcon => Status switch
    {
        "in_progress" => "🟢",
        "paused" => "⏸️",
        "blocked" => "🔴",
        "done" => "✅",
        _ => "⚫"
    };

    private string _lastActivity = "暂无活动";
    public string LastActivity
    {
        get => _lastActivity;
        set { _lastActivity = value; OnPropertyChanged(); }
    }

    public List<string> EnabledAgents { get; set; } = new();

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private bool _isMissing;
    public bool IsMissing
    {
        get => _isMissing;
        set { _isMissing = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string DisplayName => IsMissing ? $"{Name} [路径不存在]" : Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
