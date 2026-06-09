using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VibeTracker.Core;

namespace VibeTracker.App.Services;

/// <summary>
/// Agent 配置写入：MCP 配置 + 规则片段注入。
/// </summary>
public class AgentConfigService
{
    private readonly string _installPath;
    private readonly string _projectPath;
    private readonly string _projectId;

    private const string RulesStart = "<!-- VIBE-TRACKER-START -->";
    private const string RulesEnd = "<!-- VIBE-TRACKER-END -->";
    private const string LegacyServerKey = "vibe-tracker";
    private const string WorkspaceServerKey = "vibe-tracker-workspace";

    public AgentConfigService(string installPath, string projectPath, string projectId)
    {
        _installPath = installPath;
        _projectPath = projectPath;
        _projectId = projectId;
    }

    public string McpExePath => Path.Combine(_installPath, "VibeTracker.Mcp.exe");

    private object CreateMcpServerConfig(string source) => new
    {
        command = McpExePath,
        args = new[] { "--project", _projectPath, "--source", source },
        env = new Dictionary<string, string>
        {
            ["VIBE_TRACKER_PROJECT"] = _projectPath,
            ["VIBE_TRACKER_SOURCE"] = source
        }
    };

    private object CreateWorkspaceMcpServerConfig(string source) => new
    {
        command = McpExePath,
        args = new[] { "--workspace", "--source", source },
        env = new Dictionary<string, string>
        {
            ["VIBE_TRACKER_WORKSPACE"] = "1",
            ["VIBE_TRACKER_SOURCE"] = source
        }
    };

    private static bool IsVibeTrackerServerKey(string key)
        => string.Equals(key, LegacyServerKey, StringComparison.OrdinalIgnoreCase)
           || string.Equals(key, WorkspaceServerKey, StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("vibe-tracker-", StringComparison.OrdinalIgnoreCase);

    private static string EscapeTomlString(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string RemoveVibeTrackerTomlSections(string toml)
    {
        var normalized = toml.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var kept = new List<string>();
        var skip = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                skip = trimmed.StartsWith("[mcp_servers.vibe-tracker", StringComparison.OrdinalIgnoreCase);

            if (!skip)
                kept.Add(line);
        }

        return string.Join("\n", kept).TrimEnd();
    }

    private static JsonElement? ReadExistingJsonConfig(string configPath, out string? recoveryNote)
    {
        recoveryNote = null;

        if (!File.Exists(configPath))
            return null;

        var json = File.ReadAllText(configPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException ex)
        {
            var backupPath = $"{configPath}.invalid-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(configPath, backupPath, overwrite: true);
            recoveryNote = $"原配置 JSON 无法解析，已备份到 {backupPath}，并重建 MCP 配置。错误: {ex.Message}";
            return null;
        }
    }

    // ═══════ Claude Desktop ═══════

    private (bool, string)? CheckMcpExe()
    {
        if (!File.Exists(McpExePath))
            return (false, $"MCP 服务端不存在: {McpExePath}\n请重新构建或安装 VibeTracker。");
        return null;
    }

    public (bool Success, string Message) ConfigureClaudeDesktop()
    {
        var check = CheckMcpExe();
        if (check != null) return check.Value;

        var configPath = FindClaudeDesktopConfig();
        if (configPath == null)
            return (false, "未找到 Claude Desktop 配置文件。\n请打开 Claude Desktop → Settings → Developer → Edit Config，确认文件存在后重试。");

        try
        {
            // 读取现有配置；配置损坏时先备份，再重建 MCP 配置。
            var existingConfig = ReadExistingJsonConfig(configPath, out var recoveryNote);

            // 构建或合并 mcpServers
            Dictionary<string, object> servers;
            if (existingConfig?.TryGetProperty("mcpServers", out var existingServers) == true
                && existingServers.ValueKind != JsonValueKind.Null)
            {
                servers = JsonSerializer.Deserialize<Dictionary<string, object>>(existingServers.GetRawText())!;
            }
            else
            {
                servers = new Dictionary<string, object>();
            }

            foreach (var key in servers.Keys.Where(IsVibeTrackerServerKey).ToList())
                servers.Remove(key);

            servers[WorkspaceServerKey] = CreateWorkspaceMcpServerConfig("claude");

            // 保留原配置中的其他顶层字段
            var fullConfig = new Dictionary<string, object>
            {
                ["mcpServers"] = servers
            };
            if (existingConfig.HasValue)
            {
                foreach (var prop in existingConfig.Value.EnumerateObject())
                {
                    if (prop.Name != "mcpServers")
                    {
                        try
                        {
                            fullConfig[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText())!;
                        }
                        catch { /* 跳过无法反序列化的字段 */ }
                    }
                }
            }

            var outputJson = JsonSerializer.Serialize(fullConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, outputJson, new UTF8Encoding(false));

            WriteRules("CLAUDE.md");

            // 3p 部署：同时写入用户级 CLAUDE.md
            if (configPath.Contains("Claude-3p"))
            {
                var userClaudePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Claude", "CLAUDE.md");
                WriteRulesAtPath(userClaudePath);
            }

            // MSIX 备份
            if (configPath.Contains("Packages"))
            {
                var bakDir = Path.Combine(_projectPath, ".vibe", ".bak");
                Directory.CreateDirectory(bakDir);
                File.Copy(configPath, Path.Combine(bakDir, "claude-desktop-config.json"), overwrite: true);
            }

            var message = "Claude Desktop workspace MCP 配置已写入。请完全退出并重启 Claude Desktop。";
            if (!string.IsNullOrWhiteSpace(recoveryNote))
                message += "\n" + recoveryNote;
            return (true, message);
        }
        catch (Exception ex)
        {
            return (false, $"写入 Claude Desktop 配置失败: {ex.Message}");
        }
    }

    private static string? FindClaudeDesktopConfig()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // 1. Claude-3p 路径（企业版/第三方部署）
        var c3pPath = Path.Combine(localAppData, "Claude-3p", "claude_desktop_config.json");
        if (File.Exists(c3pPath))
            return c3pPath;

        // 2. 标准 EXE 路径
        var standardPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json");
        if (File.Exists(standardPath))
            return standardPath;

        // 3. MSIX 路径
        var packagesDir = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesDir))
        {
            var claudeDirs = Directory.GetDirectories(packagesDir, "Claude_*");
            foreach (var dir in claudeDirs)
            {
                var msixConfigPath = Path.Combine(dir, "LocalCache", "Roaming", "Claude", "claude_desktop_config.json");
                if (File.Exists(msixConfigPath))
                    return msixConfigPath;
            }
        }

        return null;
    }

    // ═══════ Codex Desktop ═══════

    public (bool Success, string Message) ConfigureCodexDesktop()
    {
        var check = CheckMcpExe();
        if (check != null) return check.Value;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configPath = Path.Combine(userProfile, ".codex", "config.toml");

        try
        {
            var dir = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(dir);

            var sectionName = WorkspaceServerKey;
            var sectionHeader = $"[mcp_servers.{sectionName}]";
            var newSection = $@"
{sectionHeader}
command = ""{EscapeTomlString(McpExePath)}""
args = [""--workspace"", ""--source"", ""codex""]
env = {{ VIBE_TRACKER_WORKSPACE = ""1"", VIBE_TRACKER_SOURCE = ""codex"" }}
enabled = true
";

            string toml;
            if (File.Exists(configPath))
            {
                toml = RemoveVibeTrackerTomlSections(File.ReadAllText(configPath, Encoding.UTF8));
                toml = toml.TrimEnd() + "\n" + newSection;
            }
            else
            {
                toml = newSection.TrimStart();
            }

            File.WriteAllText(configPath, toml, new UTF8Encoding(false));

            WriteRules("AGENTS.md");

            return (true, "Codex Desktop workspace MCP 配置已写入。请完全退出并重启 Codex Desktop。");
        }
        catch (Exception ex)
        {
            return (false, $"写入 Codex Desktop 配置失败: {ex.Message}");
        }
    }

    // ═══════ Claude Code（兼容） ═══════

    public (bool Success, string Message) ConfigureClaudeCode()
    {
        var check = CheckMcpExe();
        if (check != null) return check.Value;

        try
        {
            var claudeDir = Path.Combine(_projectPath, ".claude");
            Directory.CreateDirectory(claudeDir);

            var mcpPath = Path.Combine(claudeDir, "mcp.json");
            Dictionary<string, object> mcpConfig;

            if (File.Exists(mcpPath))
            {
                mcpConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(mcpPath, Encoding.UTF8))!;

                // 合并 vibe-tracker 进现有 mcpServers，保留已有 server
                if (mcpConfig.TryGetValue("mcpServers", out var serversObj)
                    && serversObj is JsonElement serversEl
                    && serversEl.ValueKind == JsonValueKind.Object)
                {
                    var servers = JsonSerializer.Deserialize<Dictionary<string, object>>(serversEl.GetRawText())!;
                    servers["vibe-tracker"] = CreateMcpServerConfig("claude");
                    mcpConfig["mcpServers"] = servers;
                }
                else
                {
                    mcpConfig["mcpServers"] = new Dictionary<string, object>
                    {
                        ["vibe-tracker"] = CreateMcpServerConfig("claude")
                    };
                }
            }
            else
            {
                mcpConfig = new Dictionary<string, object>
                {
                    ["mcpServers"] = new Dictionary<string, object>
                    {
                        ["vibe-tracker"] = CreateMcpServerConfig("claude")
                    }
                };
            }

            var json = JsonSerializer.Serialize(mcpConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(mcpPath, json, new UTF8Encoding(false));

            WriteRules("CLAUDE.md");

            return (true, "Claude Code 配置已写入项目。");
        }
        catch (Exception ex)
        {
            return (false, $"写入 Claude Code 配置失败: {ex.Message}");
        }
    }

    // ═══════ 规则片段 ═══════

    private void WriteRules(string fileName)
    {
        var rulesPath = Path.Combine(_projectPath, fileName);
        WriteRulesAtPath(rulesPath);
    }

    private void WriteRulesAtPath(string rulesPath)
    {
        var dir = Path.GetDirectoryName(rulesPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var rules = TemplateGenerator.GetRulesContent();

        string content;
        if (File.Exists(rulesPath))
        {
            content = File.ReadAllText(rulesPath, Encoding.UTF8);

            var start = content.IndexOf(RulesStart);
            var end = content.IndexOf(RulesEnd);

            if (start >= 0 && end > start)
            {
                content = content.Remove(start, end - start + RulesEnd.Length);
                content = content.Insert(start, rules);
            }
            else
            {
                content = content.TrimEnd() + "\n\n" + rules;
            }
        }
        else
        {
            content = rules;
        }

        File.WriteAllText(rulesPath, content, new UTF8Encoding(false));
    }
}
