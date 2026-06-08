using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VibeTracker.Core;

/// <summary>
/// .mcp-calls.log 写入与读取，用于仪表盘活动指示。
/// </summary>
public class ActivityLogger
{
    private readonly FileEngine _file;

    public ActivityLogger(FileEngine file)
    {
        _file = file;
    }

    public void LogCall(string tool, string source)
    {
        var entry = new McpCallEntry
        {
            Tool = tool,
            Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Source = source
        };

        _file.WithLock(() => _file.AppendJsonLine(".mcp-calls.log", entry));
    }

    public McpCallEntry? GetLastCall()
    {
        var calls = _file.ReadJsonLinesReverse<McpCallEntry>(".mcp-calls.log", 1);
        return calls.FirstOrDefault();
    }

    /// <summary>
    /// 返回人类可读的上次活动描述。
    /// </summary>
    public string GetLastActivityText()
    {
        var last = GetLastCall();
        if (last == null)
            return "暂无活动记录";

        if (DateTime.TryParse(last.Time, out var time))
        {
            var span = DateTime.Now - time;
            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} 小时前";
            return $"{(int)span.TotalDays} 天前";
        }

        return last.Time;
    }

    public bool IsActive(TimeSpan maxIdle)
    {
        var last = GetLastCall();
        if (last == null) return false;

        if (DateTime.TryParse(last.Time, out var time))
            return (DateTime.Now - time) < maxIdle;

        return false;
    }
}

public class McpCallEntry
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "unknown";
}
