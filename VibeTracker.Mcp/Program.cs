using System;
using System.IO;

namespace VibeTracker.Mcp;

/// <summary>
/// MCP Server 入口。
/// 用法：VibeTracker.Mcp.exe --project "D:\projects\xxx" --source "claude"
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        string? projectRoot = null;
        string? source = null;

        // 解析启动参数
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--project" && i + 1 < args.Length)
                projectRoot = args[++i];
            else if (args[i] == "--source" && i + 1 < args.Length)
                source = args[++i];
        }

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            Console.Error.WriteLine("错误: 缺少 --project 参数。用法: VibeTracker.Mcp.exe --project <路径> --source <claude|codex>");
            Environment.Exit(1);
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            source = "unknown";
        }

        // 确保项目目录存在
        if (!Directory.Exists(projectRoot))
        {
            Console.Error.WriteLine($"错误: 项目路径不存在: {projectRoot}");
            Environment.Exit(1);
        }

        var vibeDir = Path.Combine(projectRoot, ".vibe");
        if (!Directory.Exists(vibeDir))
        {
            Console.Error.WriteLine($"错误: 未找到 .vibe/ 目录。请先通过 VibeTracker 初始化项目。路径: {vibeDir}");
            Environment.Exit(1);
        }

        // 启动 MCP Server
        var server = new McpServer(projectRoot, source);
        server.Run();
    }
}
