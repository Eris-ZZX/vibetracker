using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VibeTracker.Core;
using VibeTracker.Mcp.Tools;

namespace VibeTracker.Mcp;

public class WorkspaceMcpServer
{
    private readonly string _source;
    private readonly WorkspaceProjectRegistry _registry;
    private readonly Dictionary<string, string> _sessionIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMcpTool> _tools;

    public WorkspaceMcpServer(string source)
        : this(source, new WorkspaceProjectRegistry())
    {
    }

    public WorkspaceMcpServer(string source, WorkspaceProjectRegistry registry)
    {
        _source = source;
        _registry = registry;
        _tools = RegisterTools();
    }

    private Dictionary<string, IMcpTool> RegisterTools()
    {
        var tools = new Dictionary<string, IMcpTool>
        {
            ["list_projects"] = new ListProjectsTool(_registry)
        };

        foreach (var name in new[]
                 {
                     "get_context",
                     "get_plan",
                     "add_log",
                     "add_finding",
                     "update_state",
                     "get_found",
                     "get_recent_logs",
                     "check_consistency"
                 })
        {
            tools[name] = new WorkspaceProjectTool(
                name,
                CreateTool(name, CreateSchemaContext()),
                _registry,
                _source,
                _sessionIds,
                ctx => CreateTool(name, ctx));
        }

        return tools;
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
                JsonNode? idNode = root.TryGetProperty("id", out var idEl)
                    ? JsonNode.Parse(idEl.GetRawText())
                    : null;

                var response = HandleRequest(method, root, idNode);
                if (response != null)
                    writer.WriteLine(response);
            }
            catch (Exception ex)
            {
                var err = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = null,
                    ["error"] = new JsonObject { ["code"] = -32603, ["message"] = $"内部错误: {ex.Message}" }
                };
                writer.WriteLine(err.ToJsonString());
            }
        }
    }

    private string? HandleRequest(string method, JsonElement root, JsonNode? id)
    {
        switch (method)
        {
            case "initialize":
                return JsonResult(id, new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "VibeTracker Workspace", version = "1.0.0" }
                });
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
            var result = tool.Execute(arguments);
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

    private static ToolContext CreateSchemaContext()
    {
        var file = new FileEngine(Directory.GetCurrentDirectory());
        return new ToolContext(file, new ActivityLogger(file), "schema", () => "session-schema");
    }

    private static IMcpTool CreateTool(string name, ToolContext ctx)
        => name switch
        {
            "get_context" => new GetContextTool(ctx),
            "get_plan" => new GetPlanTool(ctx),
            "add_log" => new AddLogTool(ctx),
            "add_finding" => new AddFindingTool(ctx),
            "update_state" => new UpdateStateTool(ctx),
            "get_found" => new GetFoundTool(ctx),
            "get_recent_logs" => new GetRecentLogsTool(ctx),
            "check_consistency" => new CheckConsistencyTool(ctx),
            _ => throw new ArgumentException($"未知工具: {name}")
        };
}
