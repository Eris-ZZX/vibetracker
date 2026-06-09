using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using VibeTracker.Core.Models;

namespace VibeTracker.Core;

/// <summary>
/// 初始化项目时生成 .vibe/ 目录下的模板文件。
/// </summary>
public class TemplateGenerator
{
    private readonly FileEngine _file;

    public TemplateGenerator(FileEngine file)
    {
        _file = file;
    }

    public void Initialize(string projectName, string seed, List<string> tags, string? existingVibe = null)
    {
        _file.EnsureDirectories();

        // 如果已有 .vibe/，不覆盖，直接返回
        if (_file.Exists() && existingVibe == null)
            return;

        // plan.md
        var plan = GeneratePlan(projectName, seed);
        _file.WriteMarkdown("plan.md", plan);

        // state.json
        var state = new StateModel
        {
            ProjectRoot = _file.ProjectRoot,
            Status = "in_progress",
            CurrentTask = "项目初始化",
            Features = new List<FeatureItem>
            {
                new() { Id = "F1", Title = "项目初始化", Status = "in_progress" }
            },
            PendingSteps = new List<string> { "根据 seed 生成完整的 PRD & SPEC" },
            NextStep = "根据 seed 生成完整的 PRD & SPEC",
            LastAction = "项目已初始化，等待 agent 首次活动",
            Source = "human"
        };
        _file.AtomicWriteJson("state.json", state);

        // config.json
        var config = new ConfigModel
        {
            ProjectName = projectName,
            Seed = seed,
            Tags = tags
        };
        _file.AtomicWriteJson("config.json", config);

        // log.jsonl（空文件）
        File.WriteAllText(Path.Combine(_file.VibeDir, "log.jsonl"), "", new UTF8Encoding(false));

        // findings.jsonl（空文件）
        File.WriteAllText(Path.Combine(_file.VibeDir, "findings.jsonl"), "", new UTF8Encoding(false));

        // .mcp-calls.log（空文件）
        File.WriteAllText(Path.Combine(_file.VibeDir, ".mcp-calls.log"), "", new UTF8Encoding(false));
    }

    private string GeneratePlan(string projectName, string seed)
    {
        return $@"# {projectName} — PRD & SPEC

> 种子需求：{seed}
> 本文档由 agent 根据种子需求自动生成和维护。

---

# Part 1. PRD：产品需求文档

## 1. 产品定位
（待填写：描述这个产品是什么、解决什么问题）

## 2. 产品目标
| 目标 | 说明 |
|------|------|

## 3. 用户与角色
| 角色 | 主要关注 |
|------|----------|

## 4. 功能需求
- [ ] F1 项目初始化 — 搭建项目基础框架

## 5. 不做的事
- _

## 6. MVP 范围
（待填写：首期做哪些功能）

## 7. PRD 验收标准
| 验收项 | 标准 |
|--------|------|

---

# Part 2. SPEC：技术规格文档

## 1. 技术基线
| 层 | 选择 | 原因 |
|----|------|------|

## 2. 系统架构
（待填写：系统架构描述）

## 3. 核心目录结构
```
（待填写：目录树）
```

## 4. 核心数据类型
（待填写：数据模型）

## 5. 各模块详细设计
（待填写：按模块展开）

## 6. 阶段实施计划
| 阶段 | 交付重点 |
|------|----------|

## 7. SPEC 验收标准
| 验收项 | 测试方式 |
|--------|----------|

## 8. 默认假设
- _

---

# 变更记录
| 日期 | 变更 | 原因 |
|------|------|------|
";
    }

    /// <summary>
    /// 生成 Claude Desktop / Codex 规则片段。
    /// </summary>
    public static string GetRulesContent()
    {
        return "<!-- VIBE-TRACKER-START -->\n" +
"## Vibe Tracker — 过程追踪\n\n" +
"如果工具列表中有 list_projects，说明 MCP 运行在 workspace 模式：\n" +
"  1. 新会话开始先调用 list_projects，选择当前项目。\n" +
"  2. 后续每次调用 Vibe Tracker 工具都带上 projectId，例如 get_context({projectId})、get_plan({projectId})、add_log({projectId, ...})、update_state({projectId, ...})。\n" +
"如果没有 list_projects，说明 MCP 运行在单项目模式，工具调用不需要 projectId。\n\n" +
"会话开始调 get_context，了解项目状态。\n" +
"如首次运行或需要完整规划，调用 get_plan 读取 plan.md 模板。\n\n" +
"六种 add_log type 及其触发时机：\n" +
"  action   -> 完成了一个任务或操作\n" +
"  decision -> 做了技术/架构决策，必须带 reason\n" +
"  problem  -> 发现 bug 或障碍，必须带 cause，resolved 默认 false；修复后填 resolution 说明如何解决\n" +
"  next     -> 下一步计划（通常会话结束时调用一次）\n" +
"  status   -> Feature 或 Step 状态变更，e.g. \"F2: in_progress -> done\" 或 \"F1.S2: todo -> done\"，每次状态流转必记\n" +
"  change   -> 需求/功能新增、修改或取消，必须带 reason，e.g. \"新增 F5: 数据导出\"\n\n" +
"每完成一个功能后：\n" +
"  1. 打开 plan.md，勾选对应 checkbox\n" +
"  2. add_log(type=\"status\", action=\"F2: in_progress -> done\")\n" +
"  3. update_state 传该功能的 features + steps（只传变更项即可，支持增量合并）\n\n" +
"发现 bug 时：add_log(type=\"problem\", action=\"xxx\", cause=\"xxx\", resolved=false)\n" +
"修复 bug 后：\n" +
"  1. add_log(type=\"status\", action=\"Bug #xxx 已修复\")\n" +
"  2. 追加一条 add_log(type=\"problem\", action=\"[已修复] xxx\", cause=\"xxx\", resolved=true)\n" +
"  Bug 未解决列表只统计 resolved!=true 的最近记录，旧记录随日志滚动自然沉底。\n" +
"需求或功能变更（新增/修改/取消）时：add_log(type=\"change\", action=\"新增 F5: 数据导出\", reason=\"用户需要\")\n" +
"遇到经验或坑调 add_finding：{type: \"good\"|\"pit\", tag, title, body, consequence?}\n\n" +
"首次生成或大幅修改 plan.md 后，必须立刻将所有 checkbox 功能一次性全量同步到 state.json，同时把每个功能的执行步骤拆成 steps：\n" +
"  update_state({features: [{id:\"F1\", title:\"xxx\", status:\"todo\", steps: [{id:\"S1\", title:\"具体步骤1\", status:\"todo\"}, {id:\"S2\", title:\"具体步骤2\", status:\"todo\"}]}, ...]})\n" +
"  Step 由 agent 根据功能复杂度自行拆解，粒度建议 2-6 步/功能；后续每个 Step 状态变更同样调 add_log(type=\"status\", action=\"F1.S1: todo -> done\")\n\n" +
"会话结束前：\n" +
"  add_log(type=\"next\", action=\"下一步做什么\")\n" +
"  update_state({status, features, currentTask, blocker, lastAction, nextStep})\n" +
"  check_consistency。如有 warning，修正后重新 update_state。\n" +
"可用标签：@frontend @backend @devops @database @npm @bug @config @deploy @general\n" +
"<!-- VIBE-TRACKER-END -->\n";
    }
}
