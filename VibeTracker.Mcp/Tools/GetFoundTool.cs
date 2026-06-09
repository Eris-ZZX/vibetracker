using System;
using System.Linq;
using System.Text.Json;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp.Tools;

public class GetFoundTool : IMcpTool
{
    private readonly ToolContext _ctx;
    public GetFoundTool(ToolContext ctx) => _ctx = ctx;

    public string Description => "按条件查询发现记录（好经验/踩坑）。可按 type 和 tag 筛选。";
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            type = new { type = "string", description = "筛选类型: good / pit，不填则全部" },
            tag = new { type = "string", description = "筛选标签: @frontend 等，不填则全部" },
            source = new { type = "string", description = "按 agent 来源筛选: claude / codex" },
            limit = new { type = "number", description = "返回条数上限，默认 20" }
        },
        required = Array.Empty<string>()
    };

    public string Execute(JsonElement arguments)
    {
        var limit = 20;
        if (arguments.TryGetProperty("limit", out var l) && l.TryGetInt32(out var n) && n > 0)
            limit = Math.Min(n, 50); // cap

        // 多读可过滤列以覆盖 type/tag/source 过滤，有硬上限
        var typeFilter = arguments.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        var tagFilter = arguments.TryGetProperty("tag", out var tagEl) ? tagEl.GetString() : null;
        var sourceFilter = arguments.TryGetProperty("source", out var sourceEl) ? sourceEl.GetString() : null;
        var findings = _ctx.File.ReadJsonLinesReverse<FindingEntry>(
            "findings.jsonl",
            limit,
            f => (string.IsNullOrWhiteSpace(typeFilter) || f.Type == typeFilter)
                && (string.IsNullOrWhiteSpace(tagFilter) || f.Tag == tagFilter)
                && (string.IsNullOrWhiteSpace(sourceFilter) || f.Source == sourceFilter));

        var results = findings.Take(limit).Select(f => new
        {
            id = f.Id,
            type = f.Type,
            tag = f.Tag,
            title = f.Title,
            body = f.Body,
            consequence = f.Consequence,
            time = f.Time,
            source = f.Source
        });

        return JsonSerializer.Serialize(new { findings = results }, new JsonSerializerOptions { WriteIndented = true });
    }
}
