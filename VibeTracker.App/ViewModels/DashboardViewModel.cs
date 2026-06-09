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
    public List<FeatureItemViewModel> Features { get; } = new();

    private bool _isCorrupted;
    public bool IsCorrupted
    {
        get => _isCorrupted;
        set { _isCorrupted = value; OnPropertyChanged(); OnPropertyChanged(nameof(DataHint)); }
    }
    public string DataHint => IsCorrupted ? "⚠️ 数据文件异常，请检查 .vibe/state.json" : "";

    public void Refresh()
    {
        // 用 TryReadJson 检测损坏
        var (data, error) = _file.TryReadJson<StateModel>("state.json");
        if (data != null)
        {
            IsCorrupted = false;
            Status = data.Status;
            CurrentTask = data.CurrentTask;
            LastUpdate = FormatDateTime(data.UpdatedAt, includeDate: true);
            TotalFeatures = data.Features?.Count ?? 0;
            DoneFeatures = data.Features?.Count(f => f.Status == "done") ?? 0;
        }
        else
        {
            IsCorrupted = true;
            _file.ReadJson<StateModel>("state.json"); // 尝试备份恢复
            var restored = _file.TryReadJson<StateModel>("state.json");
            if (restored.Data != null)
            {
                IsCorrupted = false;
                Status = restored.Data.Status;
                CurrentTask = restored.Data.CurrentTask;
                LastUpdate = FormatDateTime(restored.Data.UpdatedAt, includeDate: true);
                TotalFeatures = restored.Data.Features?.Count ?? 0;
                DoneFeatures = restored.Data.Features?.Count(f => f.Status == "done") ?? 0;
            }
        }

        // 功能清单
        var features = data?.Features ?? new List<FeatureItem>();
        Features.Clear();
        foreach (var f in features)
        {
            var vm = new FeatureItemViewModel
            {
                Id = f.Id,
                Title = f.Title,
                Status = f.Status
            };
            if (f.Steps != null)
            {
                foreach (var s in f.Steps)
                {
                    vm.Steps.Add(new StepItemViewModel
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Status = s.Status
                    });
                }
            }
            Features.Add(vm);
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

public class StepItemViewModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "todo";
    public string StatusIcon => Status switch
    {
        "done" => "✅",
        "in_progress" => "🔄",
        "blocked" => "🚫",
        _ => "⬜"
    };
}

public class FeatureItemViewModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "todo";
    public string StatusIcon => Status switch
    {
        "done" => "✅",
        "in_progress" => "🔄",
        "blocked" => "🚫",
        "dropped" => "❌",
        _ => "⬜"
    };
    public List<StepItemViewModel> Steps { get; } = new();
    public bool HasSteps => Steps.Count > 0;
}

public class FindingItemViewModel
{
    public string Tag { get; set; } = "";
    public string Title { get; set; } = "";
}
