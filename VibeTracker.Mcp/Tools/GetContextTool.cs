using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp.Tools;

/// <summary>
/// agent 冷启动时调用，一次获取全部上下文。
/// 返回 state 摘要 + 最近日志 + 最近坑，控制在 800 tokens 内。
/// </summary>
public class GetContextTool : IMcpTool
{
    private readonly ToolContext _ctx;

    public GetContextTool(ToolContext ctx) => _ctx = ctx;

    public string Description => "获取项目当前上下文：状态摘要、进行中的功能、最近活动、最近踩坑。每次新对话开始时调用。";

    public object InputSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public string Execute(JsonElement arguments)
    {
        // 读 state：损坏时尝试备份恢复，恢复失败则报错（不返回空默认值）
        var (data, err) = _ctx.File.TryReadJson<StateModel>("state.json");
        if (data == null && err != null)
        {
            var restored = _ctx.File.TryRecoverJson<StateModel>("state.json");
            if (restored != null)
            {
                data = restored;
            }
            else
            {
                throw new InvalidOperationException(
                    $"state.json 损坏且无法从备份恢复。原始错误: {err}。" +
                    "请检查 .vibe/state.json 文件内容，或从 .vibe/.bak/ 手动恢复。");
            }
        }
        var state = data!;

        var (allLogs, corruptedLogLines) = _ctx.File.ReadJsonLinesWithStats<LogEntry>("log.jsonl");
        var (allFindings, corruptedFindingLines) = _ctx.File.ReadJsonLinesWithStats<FindingEntry>("findings.jsonl");
        var config = _ctx.File.ReadJson<ConfigModel>("config.json");
        // 功能摘要
        var features = state.Features ?? new List<FeatureItem>();
        var featureSummary = new
        {
            total = features.Count,
            done = features.Count(f => f.Status == "done"),
            inProgress = features.Count(f => f.Status == "in_progress"),
            blocked = features.Count(f => f.Status == "blocked"),
            todo = features.Count(f => f.Status == "todo")
        };

        // 进行中 & 阻塞的功能（最多 5 条）
        var activeFeatures = features
            .Where(f => f.Status == "in_progress" || f.Status == "blocked")
            .Take(5)
            .Select(f => new { id = f.Id, title = f.Title, status = f.Status });

        // 待做的功能（前 3 条）
        var nextFeatures = features
            .Where(f => f.Status == "todo")
            .Take(3)
            .Select(f => new { id = f.Id, title = f.Title });

        // 最近日志（5 条）
        var recentLogs = allLogs.Count > 5
            ? allLogs.GetRange(allLogs.Count - 5, 5).Select(l => new { type = l.Type, action = l.Action, time = l.Time, source = l.Source })
            : allLogs.Select(l => new { type = l.Type, action = l.Action, time = l.Time, source = l.Source });

        // 最近坑（3 条）
        var recentPits = _ctx.File.ReadJsonLinesReverse<FindingEntry>("findings.jsonl", 3)
            .Where(f => f.Type == "pit")
            .Select(f => new { tag = f.Tag, title = f.Title, body = f.Body, consequence = f.Consequence });

        // 未解决问题数
        var openProblemCount = allLogs.Count(l => l.Type == "problem" && l.Resolved != true);

        // 轻量一致性检查（复用已加载的 state + logs，不重复读文件）
        var quickWarnings = ConsistencyChecker.QuickCheckWith(state, allLogs);

        var result = new
        {
            state = new
            {
                status = state.Status,
                currentTask = state.CurrentTask,
                source = state.Source,
                nextStep = state.NextStep,
                blocker = state.Blocker,
                featureSummary,
                activeFeatures,
                nextFeatures,
                openProblemCount
            },
            recentLogs,
            recentPits,
            corruptedLogLines,
            corruptedFindingLines,
            warnings = quickWarnings,
            config = new
            {
                projectName = config?.ProjectName ?? "",
                seed = config?.Seed ?? "",
                contextLines = config?.AgentPreferences?.ContextLines ?? 5,
                autoCheckConsistency = config?.AgentPreferences?.AutoCheckConsistency ?? true
            }
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
