using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using VibeTracker.Core;

namespace VibeTracker.Mcp.Tools;

public class WorkspaceProjectTool : IMcpTool
{
    private readonly string _toolName;
    private readonly IMcpTool _schemaTool;
    private readonly WorkspaceProjectRegistry _registry;
    private readonly string _source;
    private readonly Dictionary<string, string> _sessionIds;
    private readonly Func<ToolContext, IMcpTool> _createTool;

    public WorkspaceProjectTool(
        string toolName,
        IMcpTool schemaTool,
        WorkspaceProjectRegistry registry,
        string source,
        Dictionary<string, string> sessionIds,
        Func<ToolContext, IMcpTool> createTool)
    {
        _toolName = toolName;
        _schemaTool = schemaTool;
        _registry = registry;
        _source = source;
        _sessionIds = sessionIds;
        _createTool = createTool;
    }

    public string Description
        => $"workspace 模式：先调用 list_projects 获取 projectId；调用本工具时必须传 projectId 或 projectPath。{_schemaTool.Description}";

    public object InputSchema => BuildWorkspaceSchema(_schemaTool.InputSchema);

    public string Execute(JsonElement arguments)
    {
        var project = _registry.Resolve(arguments);
        var file = new FileEngine(project.Path);
        var activity = new ActivityLogger(file);

        if (_toolName == "get_context")
            _sessionIds[project.Path] = NewSessionId();

        var ctx = new ToolContext(file, activity, _source, () => GetSessionId(project.Path));
        var tool = _createTool(ctx);
        var result = tool.Execute(arguments);

        activity.LogCall(_toolName, _source);
        _registry.Touch(project.Id);

        return result;
    }

    private string GetSessionId(string projectPath)
    {
        if (!_sessionIds.TryGetValue(projectPath, out var sessionId))
        {
            sessionId = NewSessionId();
            _sessionIds[projectPath] = sessionId;
        }

        return sessionId;
    }

    private static string NewSessionId()
        => $"session-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    private static JsonObject BuildWorkspaceSchema(object innerSchema)
    {
        var node = JsonSerializer.SerializeToNode(innerSchema)?.AsObject() ?? new JsonObject();
        if (node["type"] == null)
            node["type"] = "object";

        var properties = node["properties"] as JsonObject;
        if (properties == null)
        {
            properties = new JsonObject();
            node["properties"] = properties;
        }

        properties["projectId"] = new JsonObject
        {
            ["type"] = "string",
            ["description"] = "VibeTracker 项目 ID。workspace 模式推荐传此字段；先调用 list_projects 获取。"
        };
        properties["projectPath"] = new JsonObject
        {
            ["type"] = "string",
            ["description"] = "项目根目录绝对路径。projectId 不可用时可传此字段。"
        };

        return node;
    }
}
