using System.Linq;
using System.Text.Json;

namespace VibeTracker.Mcp.Tools;

public class ListProjectsTool : IMcpTool
{
    private readonly WorkspaceProjectRegistry _registry;

    public ListProjectsTool(WorkspaceProjectRegistry registry)
    {
        _registry = registry;
    }

    public string Description => "列出 VibeTracker 工作区中的所有项目。workspace 模式下先调用此工具获取 projectId，再把 projectId 传给 get_context/add_log/update_state 等工具。";

    public object InputSchema => new
    {
        type = "object",
        properties = new { },
        required = new string[] { }
    };

    public string Execute(JsonElement arguments)
    {
        var (projects, error) = _registry.LoadWithError();
        var result = new
        {
            projects = projects.Select(p => new
            {
                projectId = p.Id,
                name = p.Name,
                path = p.Path,
                tag = p.Tag,
                createdAt = p.CreatedAt,
                lastActivityAt = p.LastActivityAt,
                enabledAgents = p.EnabledAgents,
                exists = p.Exists,
                vibeReady = p.VibeReady
            }),
            usage = "后续工具调用请传 projectId；如果项目不在列表中，也可以直接传 projectPath。"
        };

        if (error != null)
        {
            return JsonSerializer.Serialize(new
            {
                projects = result.projects,
                usage = result.usage,
                indexWarning = error
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
