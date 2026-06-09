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

        // 多读 2 倍以覆盖 type/tag/source 过滤
        var readCount = Math.Min(limit * 2, 200);
        var findings = _ctx.File.ReadJsonLinesReverse<FindingEntry>("findings.jsonl", readCount);

        if (arguments.TryGetProperty("type", out var typeFilter) && typeFilter.GetString() is { Length: > 0 } tf)
            findings = findings.Where(f => f.Type == tf).ToList();

        if (arguments.TryGetProperty("tag", out var tagFilter) && tagFilter.GetString() is { Length: > 0 } tg)
            findings = findings.Where(f => f.Tag == tg).ToList();

        if (arguments.TryGetProperty("source", out var srcFilter) && srcFilter.GetString() is { Length: > 0 } s)
            findings = findings.Where(f => f.Source == s).ToList();

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
