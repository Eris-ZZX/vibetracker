using System;
using System.Text.Json;
using VibeTracker.Core;

namespace VibeTracker.Mcp.Tools;

public class GetPlanTool : IMcpTool
{
    private readonly ToolContext _ctx;
    public GetPlanTool(ToolContext ctx) => _ctx = ctx;

    public string Description => "读取完整的 plan.md（PRD & SPEC）文档内容。";
    public object InputSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public string Execute(JsonElement arguments)
    {
        var raw = _ctx.File.ReadMarkdown("plan.md");
        if (string.IsNullOrWhiteSpace(raw))
            raw = "plan.md 为空或不存在，请根据种子需求生成完整的 PRD & SPEC 文档。";

        return JsonSerializer.Serialize(new { raw }, new JsonSerializerOptions { WriteIndented = true });
    }
}
