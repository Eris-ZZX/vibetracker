using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using VibeTracker.Core;
using VibeTracker.Core.Models;
using VibeTracker.Mcp.Tools;

namespace VibeTracker.Mcp;

/// <summary>
/// MCP stdio JSON-RPC 服务器。
/// 从 stdin 读请求，处理后写 stdout。stderr 用于日志（不被 agent 消费）。
/// </summary>
public class McpServer
{
    private readonly string _projectRoot;
    private readonly string _source;
    private readonly FileEngine _file;
    private readonly ActivityLogger _activity;
    private readonly Dictionary<string, IMcpTool> _tools;
    private string? _sessionId;

    private string SessionId
    {
        get
        {
            if (string.IsNullOrEmpty(_sessionId))
                _sessionId = $"session-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            return _sessionId;
        }
    }
    public McpServer(string projectRoot, string source)
    {
        _projectRoot = projectRoot;
        _source = source;
        _file = new FileEngine(projectRoot);
        _activity = new ActivityLogger(_file);
        _tools = RegisterTools();
    }

    private Dictionary<string, IMcpTool> RegisterTools()
    {
        var ctx = new ToolContext(_file, _activity, _source, () => SessionId);
        return new()
        {
            ["get_context"] = new GetContextTool(ctx),
            ["get_plan"] = new GetPlanTool(ctx),
            ["add_log"] = new AddLogTool(ctx),
            ["add_finding"] = new AddFindingTool(ctx),
            ["update_state"] = new UpdateStateTool(ctx),
            ["get_found"] = new GetFoundTool(ctx),
            ["get_recent_logs"] = new GetRecentLogsTool(ctx),
            ["check_consistency"] = new CheckConsistencyTool(ctx)
        };
    }

    public void Run()
    {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();

        using var reader = new StreamReader(stdin);
        using var writer = new StreamWriter(stdout) { AutoFlush = true };

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var method = root.GetProperty("method").GetString() ?? "";

                // 保留请求 id 的原始 JSON 表示（数字保持数字，字符串保持字符串）
                JsonNode? idNode = root.TryGetProperty("id", out var idEl)
                    ? JsonNode.Parse(idEl.GetRawText())
                    : null;

                var response = HandleRequest(method, root, idNode);
                if (response != null)
                    writer.WriteLine(response);
            }
            catch (Exception ex)
            {
                var err = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = null, ["error"] = new JsonObject { ["code"] = -32603, ["message"] = $"内部错误: {ex.Message}" } };
                writer.WriteLine(err.ToJsonString());
            }
        }
    }

    private string? HandleRequest(string method, JsonElement root, JsonNode? id)
    {
        switch (method)
        {
            case "initialize":
                return JsonResult(id, new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "VibeTracker", version = "1.0.0" } });
            case "notifications/initialized":
                return null;
            case "tools/list":
                return ListTools(id);
            case "tools/call":
                return HandleToolsCall(root, id);
            default:
                if (id == null)
                    return null;
                return JsonError(id, -32601, $"未知方法: {method}");
        }
    }

    private string ListTools(JsonNode? id)
    {
        var toolDefs = new List<object>();
        foreach (var (name, tool) in _tools)
            toolDefs.Add(new { name, description = tool.Description, inputSchema = tool.InputSchema });

        return JsonResult(id, new { tools = toolDefs });
    }

    // ═══════ JSON-RPC 响应辅助 ═══════
    // 用 JsonNode 构建响应以确保 id 保持原始类型 (number → number, string → string)

    private string JsonResult(JsonNode? id, object result)
    {
        var resp = new JsonObject { ["jsonrpc"] = "2.0" };
        if (id != null) resp["id"] = id.DeepClone();
        resp["result"] = JsonSerializer.SerializeToNode(result);
        return resp.ToJsonString();
    }

    private string JsonError(JsonNode? id, int code, string message)
    {
        var resp = new JsonObject { ["jsonrpc"] = "2.0" };
        if (id != null) resp["id"] = id.DeepClone();
        resp["error"] = new JsonObject { ["code"] = code, ["message"] = message };
        return resp.ToJsonString();
    }

    private string HandleToolsCall(JsonElement root, JsonNode? id)
    {
        var toolName = root.GetProperty("params").GetProperty("name").GetString() ?? "";
        var arguments = root.GetProperty("params").TryGetProperty("arguments", out var args)
            ? args
            : JsonSerializer.Deserialize<JsonElement>("{}");

        if (!_tools.TryGetValue(toolName, out var tool))
            return JsonError(id, -32602, $"未知工具: {toolName}");

        try
        {
            if (toolName == "get_context")
                _sessionId = $"session-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            var result = tool.Execute(arguments);
            _activity.LogCall(toolName, _source);

            return JsonResult(id, new
            {
                content = new[] { new { type = "text", text = result } }
            });
        }
        catch (Exception ex)
        {
            return JsonResult(id, new
            {
                content = new[] { new { type = "text", text = $"工具执行出错: {ex.Message}" } },
                isError = true
            });
        }
    }
}
