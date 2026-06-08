using System;
using System.Text.Json;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp.Tools;

public class AddFindingTool : IMcpTool
{
    private readonly ToolContext _ctx;

    public AddFindingTool(ToolContext ctx) => _ctx = ctx;

    public string Description => @"添加一条经验或踩坑记录。
good: {type:""good"", tag, title, body}
pit: {type:""pit"", tag, title, body, consequence?}
标签: @frontend @backend @devops @database @npm @bug @config @deploy @general";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            type = new { type = "string", description = "good=好经验, pit=踩坑" },
            tag = new { type = "string", description = "标签，如 @frontend @npm @bug" },
            title = new { type = "string", description = "一句话标题" },
            body = new { type = "string", description = "详细描述" },
            consequence = new { type = "string", description = "(pit 可选) 造成的后果" }
        },
        required = new[] { "type", "tag", "title", "body" }
    };

    public string Execute(JsonElement arguments)
    {
        var entry = new FindingEntry
        {
            Id = IdGenerator.NewId(),
            Type = arguments.GetProperty("type").GetString() ?? "pit",
            Tag = arguments.GetProperty("tag").GetString() ?? "@general",
            Title = arguments.GetProperty("title").GetString() ?? "",
            Body = arguments.GetProperty("body").GetString() ?? "",
            Source = _ctx.Source,
            Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")
        };

        if (arguments.TryGetProperty("consequence", out var cons))
            entry.Consequence = cons.GetString();

        var validator = new SchemaValidator();
        var validation = validator.ValidateFindingEntry(entry);
        if (!validation.Valid)
            throw new ArgumentException($"校验失败: {string.Join("; ", validation.Errors)}");

        var id = _ctx.File.WithLock(() => _ctx.File.AppendJsonLine("findings.jsonl", entry, entry.Id));

        return JsonSerializer.Serialize(new { id }, new JsonSerializerOptions { WriteIndented = true });
    }
}
