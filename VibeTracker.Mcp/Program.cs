using System;
using System.IO;

namespace VibeTracker.Mcp;

/// <summary>
/// MCP Server 入口。
/// 用法：VibeTracker.Mcp.exe --project "D:\projects\xxx" --source "claude"
/// 省略 --project 时使用当前工作目录。
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        if (ProjectRootResolver.IsWorkspaceMode(args))
        {
            var workspaceServer = new WorkspaceMcpServer(ProjectRootResolver.ResolveSource(args));
            workspaceServer.Run();
            return;
        }

        var resolution = ProjectRootResolver.Resolve(args);
        var projectRoot = resolution.ProjectRoot;
        var source = resolution.Source;

        if (!Directory.Exists(projectRoot))
        {
            Console.Error.WriteLine($"错误: 项目路径不存在: {projectRoot}");
            Environment.Exit(1);
        }

        // 检查 .vibe/ 目录（全局注册时不强制要求，由各 tool 自行处理）
        var vibeDir = Path.Combine(projectRoot, ".vibe");
        if (!Directory.Exists(vibeDir))
        {
            // 全局注册时不报错退出，让 tool 调用时返回友好提示
        }

        var server = new McpServer(projectRoot, source);
        server.Run();
    }
}
