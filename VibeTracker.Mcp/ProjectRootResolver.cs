using System;
using System.IO;
using System.Linq;

namespace VibeTracker.Mcp;

public sealed record ProjectRootResolution(
    string ProjectRoot,
    string Source,
    string CurrentDirectory,
    string? ExplicitProject,
    string? EnvironmentProject,
    bool FoundByWalking);

public static class ProjectRootResolver
{
    private static readonly string[] ProjectEnvironmentVariables =
    [
        "VIBE_TRACKER_PROJECT",
        "VIBE_TRACKER_PROJECT_ROOT",
        "VIBETRACKER_PROJECT_ROOT"
    ];

    public static ProjectRootResolution Resolve(string[] args)
        => Resolve(args, Directory.GetCurrentDirectory, Environment.GetEnvironmentVariable);

    public static bool IsWorkspaceMode(string[] args)
        => HasArg(args, "--workspace")
           || string.Equals(Environment.GetEnvironmentVariable("VIBE_TRACKER_WORKSPACE"), "1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(Environment.GetEnvironmentVariable("VIBE_TRACKER_WORKSPACE"), "true", StringComparison.OrdinalIgnoreCase);

    public static string ResolveSource(string[] args)
        => GetArgValue(args, "--source")
           ?? Environment.GetEnvironmentVariable("VIBE_TRACKER_SOURCE")
           ?? "unknown";

    public static ProjectRootResolution Resolve(
        string[] args,
        Func<string> currentDirectoryProvider,
        Func<string, string?> environmentProvider)
    {
        var explicitProject = GetArgValue(args, "--project");
        var source = GetArgValue(args, "--source")
            ?? environmentProvider("VIBE_TRACKER_SOURCE")
            ?? "unknown";
        var currentDirectory = Path.GetFullPath(currentDirectoryProvider());

        if (!string.IsNullOrWhiteSpace(explicitProject))
        {
            return new ProjectRootResolution(
                NormalizePath(explicitProject),
                source,
                currentDirectory,
                explicitProject,
                null,
                FoundByWalking: false);
        }

        foreach (var variable in ProjectEnvironmentVariables)
        {
            var value = environmentProvider(variable);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            return new ProjectRootResolution(
                NormalizePath(value),
                source,
                currentDirectory,
                null,
                value,
                FoundByWalking: false);
        }

        var containingProject = FindContainingVibeProject(currentDirectory);
        if (containingProject != null)
        {
            return new ProjectRootResolution(
                containingProject,
                source,
                currentDirectory,
                null,
                null,
                FoundByWalking: true);
        }

        return new ProjectRootResolution(
            currentDirectory,
            source,
            currentDirectory,
            null,
            null,
            FoundByWalking: false);
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static bool HasArg(string[] args, string name)
        => args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim().Trim('"');
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(trimmed));
    }

    private static string? FindContainingVibeProject(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".vibe")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
