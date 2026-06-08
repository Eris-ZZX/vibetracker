using System;
using System.Text.Json;
using VibeTracker.Core;

namespace VibeTracker.Mcp.Tools;

/// <summary>
/// 所有 MCP Tool 共享的上下文。
/// </summary>
public class ToolContext
{
    public FileEngine File { get; }
    public ActivityLogger Activity { get; }
    public string Source { get; }
    public Func<string> GetSessionId { get; }

    public ToolContext(FileEngine file, ActivityLogger activity, string source, Func<string> getSessionId)
    {
        File = file;
        Activity = activity;
        Source = source;
        GetSessionId = getSessionId;
    }
}

/// <summary>
/// MCP Tool 接口。
/// </summary>
public interface IMcpTool
{
    string Description { get; }
    object InputSchema { get; }
    string Execute(JsonElement arguments);
}
