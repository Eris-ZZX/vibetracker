using System;
using System.Collections.Generic;
using System.Text.Json;
using VibeTracker.Core.Models;

namespace VibeTracker.Core;

/// <summary>
/// JSON/JSONL 数据校验器。
/// P0 手写校验逻辑，不引入 ajv 等外部依赖。
/// </summary>
public class SchemaValidator
{
    public record ValidationResult(bool Valid, List<string> Errors);

    public ValidationResult ValidateState(StateModel state)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(state.SchemaVersion))
            errors.Add("state.json 缺少 schemaVersion 字段");
        if (string.IsNullOrWhiteSpace(state.ProjectRoot))
            errors.Add("state.json 缺少 projectRoot 字段");
        if (string.IsNullOrWhiteSpace(state.UpdatedAt))
            errors.Add("state.json 缺少 updatedAt 字段");

        var validStatuses = new[] { "in_progress", "paused", "blocked", "done" };
        if (Array.IndexOf(validStatuses, state.Status) < 0)
            errors.Add($"state.json status 值无效: {state.Status}，有效值: {string.Join(", ", validStatuses)}");

        if (state.Features != null)
        {
            var validFeatureStatuses = new[] { "todo", "in_progress", "done", "blocked", "dropped" };
            foreach (var f in state.Features)
            {
                if (string.IsNullOrWhiteSpace(f.Id))
                    errors.Add("state.json features 中存在缺少 id 的功能项");
                if (Array.IndexOf(validFeatureStatuses, f.Status) < 0)
                    errors.Add($"state.json features[{f.Id}] status 值无效: {f.Status}");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    public ValidationResult ValidateLogEntry(LogEntry entry)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entry.Id))
            errors.Add("log 条目缺少 id");
        if (string.IsNullOrWhiteSpace(entry.Action))
            errors.Add("log 条目缺少 action");
        if (string.IsNullOrWhiteSpace(entry.Source))
            errors.Add("log 条目缺少 source");

        var validTypes = new[] { "action", "decision", "problem", "next" };
        if (Array.IndexOf(validTypes, entry.Type) < 0)
            errors.Add($"log 条目 type 无效: {entry.Type}");

        // decision 必须有 reason
        if (entry.Type == "decision" && string.IsNullOrWhiteSpace(entry.Reason))
            errors.Add("decision 类型 log 必须填写 reason");

        // problem 必须有 cause
        if (entry.Type == "problem" && string.IsNullOrWhiteSpace(entry.Cause))
            errors.Add("problem 类型 log 必须填写 cause");

        return new ValidationResult(errors.Count == 0, errors);
    }

    public ValidationResult ValidateFindingEntry(FindingEntry entry)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entry.Id))
            errors.Add("finding 条目缺少 id");
        if (string.IsNullOrWhiteSpace(entry.Title))
            errors.Add("finding 条目缺少 title");
        if (string.IsNullOrWhiteSpace(entry.Tag))
            errors.Add("finding 条目缺少 tag");

        var validTypes = new[] { "good", "pit" };
        if (Array.IndexOf(validTypes, entry.Type) < 0)
            errors.Add($"finding 条目 type 无效: {entry.Type}");

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 校验 JSON 字符串是否可以反序列化为目标类型。
    /// </summary>
    public static bool IsValidJson<T>(string json)
    {
        try
        {
            JsonSerializer.Deserialize<T>(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
