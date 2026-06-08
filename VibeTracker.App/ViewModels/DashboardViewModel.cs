using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.App.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly FileEngine _file;
    private readonly ActivityLogger _activity;

    public DashboardViewModel(string projectPath)
    {
        _file = new FileEngine(projectPath);
        _activity = new ActivityLogger(_file);
        Refresh();
    }

    public string ProjectName { get; set; } = "";
    public string StatusIcon => Status switch
    {
        "in_progress" => "🟢 进行中",
        "paused" => "⏸️ 已暂停",
        "blocked" => "🔴 已阻塞",
        "done" => "✅ 已完成",
        _ => "⚫ 未知"
    };

    private string _status = "in_progress";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); }
    }

    private int _totalFeatures;
    public int TotalFeatures
    {
        get => _totalFeatures;
        set { _totalFeatures = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    private int _doneFeatures;
    public int DoneFeatures
    {
        get => _doneFeatures;
        set { _doneFeatures = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(ProgressPercent)); }
    }

    public string ProgressText => $"{DoneFeatures}/{TotalFeatures}";
    public double ProgressPercent => TotalFeatures > 0 ? (double)DoneFeatures / TotalFeatures * 100 : 0;

    private string _currentTask = "";
    public string CurrentTask
    {
        get => _currentTask;
        set { _currentTask = value; OnPropertyChanged(); }
    }

    private string _lastUpdate = "";
    public string LastUpdate
    {
        get => _lastUpdate;
        set { _lastUpdate = value; OnPropertyChanged(); }
    }

    private string _agentActivity = "⚫ 暂无活动";
    public string AgentActivity
    {
        get => _agentActivity;
        set { _agentActivity = value; OnPropertyChanged(); }
    }

    public List<LogItemViewModel> RecentLogs { get; } = new();
    public List<FindingItemViewModel> OpenProblems { get; } = new();

    public void Refresh()
    {
        var state = _file.ReadJson<StateModel>("state.json");
        if (state != null)
        {
            Status = state.Status;
            CurrentTask = state.CurrentTask;
            LastUpdate = FormatDateTime(state.UpdatedAt, includeDate: true);
            TotalFeatures = state.Features?.Count ?? 0;
            DoneFeatures = state.Features?.Count(f => f.Status == "done") ?? 0;
        }

        // 最近日志
        var logs = _file.ReadJsonLinesReverse<LogEntry>("log.jsonl", 5);
        RecentLogs.Clear();
        foreach (var l in logs)
        {
            RecentLogs.Add(new LogItemViewModel
            {
                Time = l.Time,
                Source = l.Source,
                Action = l.Action,
                Type = l.Type
            });
        }

        // 未解决的坑
        var allLogs = _file.ReadJsonLines<LogEntry>("log.jsonl");
        var problems = allLogs.Where(l => l.Type == "problem" && l.Resolved != true).ToList();
        OpenProblems.Clear();
        foreach (var p in problems.Take(5))
        {
            OpenProblems.Add(new FindingItemViewModel
            {
                Tag = "@problem",
                Title = p.Action
            });
        }

        // 活动状态
        var lastCall = _activity.GetLastCall();
        if (lastCall != null)
        {
            var text = _activity.GetLastActivityText();
            AgentActivity = $"🟢 {text} ({lastCall.Source})";
        }
    }

    private static string FormatDateTime(string value, bool includeDate)
    {
        if (!DateTime.TryParse(value, out var parsed))
            return value;

        return includeDate
            ? parsed.ToString("yyyy-MM-dd HH:mm")
            : parsed.ToString("HH:mm");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class LogItemViewModel
{
    public string Time { get; set; } = "";
    public string DisplayTime
    {
        get
        {
            if (DateTime.TryParse(Time, out var parsed))
                return parsed.ToString("HH:mm");
            return Time;
        }
    }
    public string Source { get; set; } = "";
    public string Action { get; set; } = "";
    public string Type { get; set; } = "";
    public string TypeIcon => Type switch
    {
        "action" => "📋",
        "decision" => "💡",
        "problem" => "⚠️",
        "next" => "➡️",
        _ => "📋"
    };
}

public class FindingItemViewModel
{
    public string Tag { get; set; } = "";
    public string Title { get; set; } = "";
}
