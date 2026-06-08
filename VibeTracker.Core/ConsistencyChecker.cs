using System;
using System.Collections.Generic;
using System.Linq;
using VibeTracker.Core.Models;

namespace VibeTracker.Core;

/// <summary>
/// state.json 与 log.jsonl 的一致性检查。
/// </summary>
public class ConsistencyChecker
{
    public record ConsistencyResult(
        bool Consistent,
        List<string> Warnings,
        List<string> Suggestions
    );

    private readonly FileEngine _file;

    public ConsistencyChecker(FileEngine file)
    {
        _file = file;
    }

    public ConsistencyResult Check()
    {
        var warnings = new List<string>();
        var suggestions = new List<string>();

        var state = _file.ReadJson<StateModel>("state.json");
        var allLogs = _file.ReadJsonLines<LogEntry>("log.jsonl");
        var allFindings = _file.ReadJsonLines<FindingEntry>("findings.jsonl");

        // 1. 状态过期检测
        if (!string.IsNullOrEmpty(state.UpdatedAt) && allLogs.Count > 0)
        {
            var lastLog = allLogs[^1];
            if (DateTime.TryParse(state.UpdatedAt, out var stateTime) &&
                DateTime.TryParse(lastLog.Time, out var logTime))
            {
                var gap = stateTime - logTime;
                if (Math.Abs(gap.TotalHours) > 1 &&
                    state.Status != "paused" && state.Status != "done")
                {
                    warnings.Add("状态可能过期：state 更新时间与最新 log 时间相差超过 1 小时");
                }
            }
        }

        // 2. nextStep 一致性
        var lastNextLog = allLogs.FindLast(l => l.Type == "next");
        if (lastNextLog != null && !string.IsNullOrEmpty(state.NextStep) &&
            !string.IsNullOrEmpty(lastNextLog.Action))
        {
            // 简单比较（忽略细微差异）
            if (!state.NextStep.Contains(lastNextLog.Action) &&
                !lastNextLog.Action.Contains(state.NextStep))
            {
                warnings.Add($"下一步描述矛盾：state.nextStep=\"{state.NextStep}\"，最新 log next=\"{lastNextLog.Action}\"");
            }
        }

        // 3. blocked 状态但无 blocker
        if (state.Status == "blocked" && string.IsNullOrWhiteSpace(state.Blocker))
            warnings.Add("项目状态为 blocked，但未填写阻塞原因（blocker 为空）");

        // 4. 未解决的 problem
        var unresolvedProblems = allLogs
            .Where(l => l.Type == "problem" && l.Resolved != true)
            .ToList();

        if (unresolvedProblems.Count > 0)
            warnings.Add($"有 {unresolvedProblems.Count} 个未解决的问题");

        // 5. features 数据完整性
        if (state.Features == null || state.Features.Count == 0)
            warnings.Add("功能进度数据缺失或损坏：features 为空");
        else
        {
            // 检查 features 和步骤摘要是否明显矛盾
            var featuresDone = state.Features.Count(f => f.Status == "done");
            var summaryDone = state.CompletedSteps?.Count ?? 0;

            if (featuresDone < summaryDone)
                suggestions.Add(
                    $"state.features 完成数 ({featuresDone}) 小于 completedSteps ({summaryDone})，建议同步");
        }

        // 6. features 状态合法性
        if (state.Features != null)
        {
            var validStatuses = new[] { "todo", "in_progress", "done", "blocked", "dropped" };
            foreach (var f in state.Features)
            {
                if (Array.IndexOf(validStatuses, f.Status) < 0)
                    warnings.Add($"feature[{f.Id}] 状态值无效: {f.Status}");
            }
        }

        return new ConsistencyResult(
            warnings.Count == 0,
            warnings,
            suggestions
        );
    }

    /// <summary>
    /// 轻量检查：仅比较时间戳，用于 get_context() 时快速判断。
    /// </summary>
    public List<string> QuickCheck()
    {
        var warnings = new List<string>();
        var state = _file.ReadJson<StateModel>("state.json");
        var allLogs = _file.ReadJsonLines<LogEntry>("log.jsonl");

        if (!string.IsNullOrEmpty(state.UpdatedAt) && allLogs.Count > 0)
        {
            var lastLog = allLogs[^1];
            if (DateTime.TryParse(state.UpdatedAt, out var stateTime) &&
                DateTime.TryParse(lastLog.Time, out var logTime))
            {
                if (Math.Abs((stateTime - logTime).TotalHours) > 1 &&
                    state.Status != "paused" && state.Status != "done")
                {
                    warnings.Add("状态可能过期：建议检查并更新 state");
                }
            }
        }

        return warnings;
    }
}
