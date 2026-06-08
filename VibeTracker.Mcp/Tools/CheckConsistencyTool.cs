using System;
using System.Text.Json;
using VibeTracker.Core;

namespace VibeTracker.Mcp.Tools;

public class CheckConsistencyTool : IMcpTool
{
    private readonly ToolContext _ctx;
    public CheckConsistencyTool(ToolContext ctx) => _ctx = ctx;

    public string Description => "检查 state.json 和 log.jsonl 是否一致。如有 warning，修正后重新 update_state。";
    public object InputSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public string Execute(JsonElement arguments)
    {
        var checker = new ConsistencyChecker(_ctx.File);
        var result = checker.Check();

        return JsonSerializer.Serialize(new
        {
            consistent = result.Consistent,
            warnings = result.Warnings,
            suggestions = result.Suggestions
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
