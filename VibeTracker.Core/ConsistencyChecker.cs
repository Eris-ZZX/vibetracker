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
        List<string> Suggestions,
        List<LogEntry> UnresolvedProblems
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

        // 用 TryReadJson 检测损坏并尝试恢复
        StateModel state;
        var (data, err) = _file.TryReadJson<StateModel>("state.json");
        if (data == null && err != null)
        {
            var restored = _file.TryRecoverJson<StateModel>("state.json");
            if (restored != null)
            {
                data = restored;
                warnings.Add("state.json 已从备份恢复，建议验证数据完整性");
            }
            else
            {
                state = new StateModel();
                warnings.Add("state.json 损坏且无法从备份恢复，一致性检查结果不可靠");
            }
        }
        state = data ?? new StateModel();

        var (allLogs, corruptedLogs) = _file.ReadJsonLinesWithStats<LogEntry>("log.jsonl");
        var (allFindings, _) = _file.ReadJsonLinesWithStats<FindingEntry>("findings.jsonl");

        if (corruptedLogs > 0)
            warnings.Add($"log.jsonl 中有 {corruptedLogs} 行损坏数据被跳过");

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
            warnings.Add($"有 {unresolvedProblems.Count} 个未解决的问题（详情见 unresolvedProblems 字段）");

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

        // 6. features 状态合法性 + Step 级校验
        if (state.Features != null)
        {
            var validFeatureStatuses = new[] { "todo", "in_progress", "done", "blocked", "dropped" };
            var validStepStatuses = new[] { "todo", "in_progress", "done", "blocked" };
            var totalSteps = 0;
            var doneSteps = 0;

            foreach (var f in state.Features)
            {
                if (Array.IndexOf(validFeatureStatuses, f.Status) < 0)
                    warnings.Add($"feature[{f.Id}] 状态值无效: {f.Status}");

                if (f.Steps != null)
                {
                    var featureDoneSteps = 0;
                    foreach (var s in f.Steps)
                    {
                        totalSteps++;
                        if (Array.IndexOf(validStepStatuses, s.Status) < 0)
                            warnings.Add($"feature[{f.Id}].steps[{s.Id}] 状态值无效: {s.Status}");

                        if (s.Status == "done") { doneSteps++; featureDoneSteps++; }

                        // Step 全完成但 Feature 未标记 done
                        if (featureDoneSteps == f.Steps.Count && f.Steps.Count > 0 && f.Status != "done" && f.Status != "blocked")
                            suggestions.Add($"feature[{f.Id}] 的所有 steps 已完成但 feature 状态为 {f.Status}，建议设为 done");

                        // Step 有 blocked 但 Feature 未标记
                        if (s.Status == "blocked" && f.Status != "blocked")
                            suggestions.Add($"feature[{f.Id}].steps[{s.Id}] 为 blocked 但 feature 状态为 {f.Status}，建议同步");
                    }

                    // Feature done 但 steps 未全完成
                    if (f.Status == "done" && featureDoneSteps < f.Steps.Count)
                        warnings.Add($"feature[{f.Id}] 状态为 done 但只有 {featureDoneSteps}/{f.Steps.Count} 个 steps 完成");
                }
            }

            if (totalSteps > 0 && doneSteps > 0)
            {
                var summaryDone = state.CompletedSteps?.Count ?? 0;
                if (doneSteps < summaryDone)
                    suggestions.Add($"step 完成数 ({doneSteps}) 小于 completedSteps ({summaryDone})，建议同步");
            }
        }

        return new ConsistencyResult(
            warnings.Count == 0,
            warnings,
            suggestions,
            unresolvedProblems
        );
    }

    /// <summary>
    /// 轻量检查：仅比较时间戳，用于 get_context() 时快速判断。
    /// </summary>
    public List<string> QuickCheck()
    {
        var warnings = new List<string>();
        var (data, err) = _file.TryReadJson<StateModel>("state.json");
        StateModel state;
        if (data == null && err != null)
        {
            var restored = _file.TryRecoverJson<StateModel>("state.json");
            if (restored != null) data = restored;
        }
        state = data ?? new StateModel();
        var (allLogs, _) = _file.ReadJsonLinesWithStats<LogEntry>("log.jsonl");
        return QuickCheckCore(state, allLogs, warnings);
    }

    /// <summary>
    /// 轻量检查，复用已加载的 state 和 logs（避免 get_context 重复读文件）。
    /// </summary>
    public static List<string> QuickCheckWith(StateModel state, List<LogEntry> allLogs)
    {
        return QuickCheckCore(state, allLogs, new List<string>());
    }

    private static List<string> QuickCheckCore(StateModel state, List<LogEntry> allLogs, List<string> warnings)
    {
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
