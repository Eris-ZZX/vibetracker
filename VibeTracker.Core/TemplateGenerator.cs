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
            Version = 0,
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
{{一句话描述 + 核心价值}}

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
{{首期做哪些}}

## 7. PRD 验收标准
| 验收项 | 标准 |
|--------|------|

---

# Part 2. SPEC：技术规格文档

## 1. 技术基线
| 层 | 选择 | 原因 |
|----|------|------|

## 2. 系统架构
{{架构描述}}

## 3. 核心目录结构
```
{{目录树}}
```

## 4. 核心数据类型
{{数据模型}}

## 5. 各模块详细设计
{{按模块展开}}

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
        return @"<!-- VIBE-TRACKER-START -->
## Vibe Tracker — 过程追踪

会话开始调 get_context（无参数），了解项目状态。
每完成一个有意义事项调 add_log：
  action → {type, action}
  decision → {type, action, reason}
  problem → {type, action, cause, resolved: false}
  next → {type, action}
每完成一个功能后，同步更新 plan.md 的 checkbox 并调 update_state：
  打开 plan.md → 勾选对应功能 → update_state 传 features 数组
遇到经验或坑调 add_finding：
  {type: ""good""|""pit"", tag, title, body, consequence?}
新增功能或调整范围时，在 plan.md 增加条目并调 update_state 追加 features
会话结束前依次调用：
  add_log(type=""next"", action=""下一步做什么"")
  update_state({status, currentTask, completedSteps, inProgressSteps, pendingSteps, blocker, lastAction, nextStep})
  check_consistency。如有 warning，修正后重新 update_state。
可用标签：@frontend @backend @devops @database @npm @bug @config @deploy @general
<!-- VIBE-TRACKER-END -->
";
    }
}
