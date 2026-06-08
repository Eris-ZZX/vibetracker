using System;
using System.Linq;
using System.Text.Json;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp.Tools;

public class GetRecentLogsTool : IMcpTool
{
    private readonly ToolContext _ctx;
    public GetRecentLogsTool(ToolContext ctx) => _ctx = ctx;

    public string Description => "获取最近 N 条日志，默认 10 条。";
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            n = new { type = "number", description = "返回条数，默认 10" },
            source = new { type = "string", description = "按 agent 来源筛选: claude / codex" }
        },
        required = Array.Empty<string>()
    };

    public string Execute(JsonElement arguments)
    {
        int n = 10;
        if (arguments.TryGetProperty("n", out var nEl) && nEl.TryGetInt32(out var val))
            n = val;

        var logs = _ctx.File.ReadJsonLinesReverse<LogEntry>("log.jsonl", int.MaxValue);

        if (arguments.TryGetProperty("source", out var src) && src.GetString() is { Length: > 0 } s)
            logs = logs.Where(l => l.Source == s).ToList();

        logs = logs.TakeLast(n).ToList();

        var results = logs.Select(l => new
        {
            id = l.Id,
            type = l.Type,
            action = l.Action,
            time = l.Time,
            source = l.Source,
            sessionId = l.SessionId,
            reason = l.Reason,
            cause = l.Cause,
            resolution = l.Resolution,
            resolved = l.Resolved
        });

        return JsonSerializer.Serialize(new { logs = results }, new JsonSerializerOptions { WriteIndented = true });
    }
}
