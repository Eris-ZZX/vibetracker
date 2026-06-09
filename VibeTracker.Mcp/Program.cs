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
        string? projectRoot = null;
        string? source = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--project" && i + 1 < args.Length)
                projectRoot = args[++i];
            else if (args[i] == "--source" && i + 1 < args.Length)
                source = args[++i];
        }

        // 无 --project 时回退到当前工作目录（Claude Code 等会把 CWD 设为项目根）
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            projectRoot = Directory.GetCurrentDirectory();
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            source = "unknown";
        }

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
