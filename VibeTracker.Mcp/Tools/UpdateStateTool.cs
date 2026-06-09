using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VibeTracker.Core;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp.Tools;

public class UpdateStateTool : IMcpTool
{
    private readonly ToolContext _ctx;
    public UpdateStateTool(ToolContext ctx) => _ctx = ctx;

    public string Description => @"更新项目状态快照。所有参数均为可选，只更新提供的字段。
status: in_progress/paused/blocked/done
features: 功能列表 [{id, title, status}] 用于更新进度
常用组合: {status, currentTask, completedSteps, inProgressSteps, pendingSteps, blocker, lastAction, nextStep}";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            status = new { type = "string", description = "in_progress / paused / blocked / done" },
            currentTask = new { type = "string" },
            completedSteps = new { type = "array", items = new { type = "string" } },
            inProgressSteps = new { type = "array", items = new { type = "string" } },
            pendingSteps = new { type = "array", items = new { type = "string" } },
            blocker = new { type = "string" },
            lastAction = new { type = "string" },
            nextStep = new { type = "string" },
            features = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string" },
                        title = new { type = "string" },
                        status = new { type = "string" }
                    }
                }
            }
        },
        required = Array.Empty<string>()
    };

    public string Execute(JsonElement arguments)
    {
        _ctx.File.WithLock(() =>
        {
            // 读取当前 state；如果文件存在但 JSON 损坏，用 TryReadJson 防静默覆盖
            var stateResult = _ctx.File.TryReadJson<StateModel>("state.json");
            StateModel state;
            if (stateResult.Data == null && stateResult.Error != null)
            {
                // state.json 损坏且无法从备份恢复 → 拒绝写入，返回错误
                throw new InvalidOperationException(
                    $"state.json 文件损坏，且无法从备份恢复。原始错误: {stateResult.Error}。" +
                    "请检查 .vibe/state.json 文件内容，或从 .vibe/.bak/ 手动恢复。");
            }
            state = stateResult.Data ?? new StateModel();

            // 合并更新
            if (arguments.TryGetProperty("status", out var status))
                state.Status = status.GetString() ?? state.Status;
            if (arguments.TryGetProperty("currentTask", out var ct))
                state.CurrentTask = ct.GetString() ?? state.CurrentTask;
            if (arguments.TryGetProperty("blocker", out var blk))
                state.Blocker = blk.GetString();
            if (arguments.TryGetProperty("lastAction", out var la))
                state.LastAction = la.GetString() ?? state.LastAction;
            if (arguments.TryGetProperty("nextStep", out var ns))
                state.NextStep = ns.GetString() ?? state.NextStep;

            if (arguments.TryGetProperty("completedSteps", out var cs) && cs.ValueKind == JsonValueKind.Array)
                state.CompletedSteps = cs.EnumerateArray().Select(e => e.GetString()!).ToList();
            if (arguments.TryGetProperty("inProgressSteps", out var ips) && ips.ValueKind == JsonValueKind.Array)
                state.InProgressSteps = ips.EnumerateArray().Select(e => e.GetString()!).ToList();
            if (arguments.TryGetProperty("pendingSteps", out var ps) && ps.ValueKind == JsonValueKind.Array)
                state.PendingSteps = ps.EnumerateArray().Select(e => e.GetString()!).ToList();

            if (arguments.TryGetProperty("features", out var feats) && feats.ValueKind == JsonValueKind.Array)
            {
                // 预校验传入项
                var items = new List<FeatureItem>();
                var ids = new HashSet<string>();
                foreach (var f in feats.EnumerateArray())
                {
                    var id = f.TryGetProperty("id", out var fid) ? fid.GetString() : null;
                    if (string.IsNullOrWhiteSpace(id))
                        throw new ArgumentException("features 数组中每项必须包含非空 id 字段");
                    if (ids.Contains(id!))
                        throw new ArgumentException($"features 中存在重复 id: {id}");
                    ids.Add(id!);

                    items.Add(new FeatureItem
                    {
                        Id = id!,
                        Title = f.TryGetProperty("title", out var ft) ? ft.GetString() ?? "" : "",
                        Status = f.TryGetProperty("status", out var fs) ? fs.GetString() ?? "todo" : "todo",
                        Steps = ParseSteps(f)
                    });
                }

                // 按 ID 合并：传入的功能更新匹配项，保留未提及的已有功能
                var incoming = items.ToDictionary(f => f.Id, f => f);

                state.Features = state.Features.Select(f =>
                {
                    if (incoming.TryGetValue(f.Id, out var update))
                    {
                        f.Status = update.Status;
                        if (!string.IsNullOrWhiteSpace(update.Title)) f.Title = update.Title;
                        if (update.Steps.Count > 0) f.Steps = update.Steps;
                        incoming.Remove(f.Id);
                    }
                    return f;
                }).Concat(incoming.Values).ToList();

                // 自动推导 steps 摘要
                state.CompletedSteps = state.Features
                    .Where(f => f.Status == "done").Select(f => f.Title).ToList();
                state.InProgressSteps = state.Features
                    .Where(f => f.Status == "in_progress").Select(f => f.Title).ToList();
                state.PendingSteps = state.Features
                    .Where(f => f.Status == "todo").Select(f => f.Title).ToList();
            }

            // 校验状态
            var validator = new SchemaValidator();
            var validation = validator.ValidateState(state);
            if (!validation.Valid)
                throw new ArgumentException($"状态校验失败: {string.Join("; ", validation.Errors)}");

            // 更新元数据
            state.Version++;
            state.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            state.Source = _ctx.Source;
            state.LastSessionId = _ctx.GetSessionId();

            // 备份 → 原子写
            _ctx.File.Backup("state.json");
            _ctx.File.AtomicWriteJson("state.json", state);
        });

        return JsonSerializer.Serialize(new { ok = true }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<StepItem> ParseSteps(JsonElement f)
    {
        if (!f.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
            return new();

        return stepsEl.EnumerateArray().Select(s =>
        {
            var step = new StepItem();
            if (s.TryGetProperty("id", out var sid)) step.Id = sid.GetString() ?? "";
            if (s.TryGetProperty("title", out var st)) step.Title = st.GetString() ?? "";
            if (s.TryGetProperty("status", out var ss)) step.Status = ss.GetString() ?? "todo";
            return step;
        }).ToList();
    }
}
