using System;
using System.Text.Json;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp.Tools;

public class AddLogTool : IMcpTool
{
    private readonly ToolContext _ctx;

    public AddLogTool(ToolContext ctx) => _ctx = ctx;

    public string Description => @"追加一条开发日志。type 必须是 action/decision/problem/next 之一。
action: {type:""action"", action}
decision: {type:""decision"", action, reason}
problem: {type:""problem"", action, cause, resolved:false, resolution?}
next: {type:""next"", action}";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            type = new { type = "string", description = "日志类型: action, decision, problem, next" },
            action = new { type = "string", description = "完成了什么 / 决策内容 / 遇到的问题 / 下一步" },
            reason = new { type = "string", description = "(decision 必填) 决策原因" },
            cause = new { type = "string", description = "(problem 必填) 问题原因" },
            resolution = new { type = "string", description = "(problem 可选) 解决方案" },
            resolved = new { type = "boolean", description = "(problem 必填) 是否已解决，默认 false" }
        },
        required = new[] { "type", "action" }
    };

    public string Execute(JsonElement arguments)
    {
        var type = arguments.GetProperty("type").GetString() ?? "action";
        var action = arguments.GetProperty("action").GetString() ?? "";

        var entry = new LogEntry
        {
            Id = IdGenerator.NewId(),
            Type = type,
            Action = action,
            Source = _ctx.Source,
            Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            SessionId = _ctx.GetSessionId()
        };

        if (arguments.TryGetProperty("reason", out var reason))
            entry.Reason = reason.GetString();
        if (arguments.TryGetProperty("cause", out var cause))
            entry.Cause = cause.GetString();
        if (arguments.TryGetProperty("resolution", out var res))
            entry.Resolution = res.GetString();
        if (arguments.TryGetProperty("resolved", out var resolved)
            && resolved.ValueKind != JsonValueKind.Null)
            entry.Resolved = resolved.GetBoolean();

        // 校验
        var validator = new SchemaValidator();
        var validation = validator.ValidateLogEntry(entry);
        if (!validation.Valid)
            throw new ArgumentException($"日志校验失败: {string.Join("; ", validation.Errors)}");

        var id = _ctx.File.WithLock(() => _ctx.File.AppendJsonLine("log.jsonl", entry, entry.Id));

        return JsonSerializer.Serialize(new { id }, new JsonSerializerOptions { WriteIndented = true });
    }
}
