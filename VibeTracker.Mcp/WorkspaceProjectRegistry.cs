using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VibeTracker.Core.Models;

namespace VibeTracker.Mcp;

public sealed record WorkspaceProject(
    string Id,
    string Name,
    string Path,
    string Tag,
    string LastActivityAt,
    bool Exists,
    bool VibeReady);

public class WorkspaceProjectRegistry
{
    private readonly string _indexPath;

    public WorkspaceProjectRegistry()
        : this(GetDefaultIndexPath())
    {
    }

    public WorkspaceProjectRegistry(string indexPath)
    {
        _indexPath = indexPath;
    }

    public IReadOnlyList<WorkspaceProject> Load()
    {
        var entries = ReadEntries();
        return entries
            .Select(e =>
            {
                var path = NormalizePath(e.Path);
                return new WorkspaceProject(
                    e.Id,
                    e.Name,
                    path,
                    e.Tag,
                    e.LastActivityAt,
                    Directory.Exists(path),
                    File.Exists(System.IO.Path.Combine(path, ".vibe", "config.json")));
            })
            .OrderByDescending(p => p.LastActivityAt)
            .ToList();
    }

    public WorkspaceProject Resolve(JsonElement arguments)
    {
        var projectId = ReadOptionalString(arguments, "projectId");
        var projectPath = ReadOptionalString(arguments, "projectPath");

        if (string.IsNullOrWhiteSpace(projectId) && string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("workspace 模式下必须传 projectId 或 projectPath。请先调用 list_projects 选择项目。");

        WorkspaceProject? project = null;
        var projects = Load();

        if (!string.IsNullOrWhiteSpace(projectId))
        {
            project = projects.FirstOrDefault(p =>
                string.Equals(p.Id, projectId, StringComparison.OrdinalIgnoreCase))
                ?? projects.FirstOrDefault(p =>
                    string.Equals(p.Name, projectId, StringComparison.OrdinalIgnoreCase));
        }

        if (project == null && !string.IsNullOrWhiteSpace(projectPath))
        {
            var normalizedPath = NormalizePath(projectPath);
            project = projects.FirstOrDefault(p =>
                string.Equals(p.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                ?? new WorkspaceProject(
                    IdFromPath(normalizedPath),
                    System.IO.Path.GetFileName(normalizedPath),
                    normalizedPath,
                    "",
                    "",
                    Directory.Exists(normalizedPath),
                    File.Exists(System.IO.Path.Combine(normalizedPath, ".vibe", "config.json")));
        }

        if (project == null)
            throw new ArgumentException($"未在 VibeTracker 工作区索引中找到项目: {projectId ?? projectPath}");

        if (!project.Exists)
            throw new InvalidOperationException($"项目路径不存在: {project.Path}");

        if (!project.VibeReady)
            throw new InvalidOperationException($"项目未初始化 .vibe/config.json: {project.Path}");

        return project;
    }

    public void Touch(string projectId)
    {
        var entries = ReadEntries();
        var entry = entries.FirstOrDefault(e => string.Equals(e.Id, projectId, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return;

        entry.LastActivityAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
        WriteEntries(entries);
    }

    private List<ProjectEntry> ReadEntries()
    {
        if (!File.Exists(_indexPath))
            return new List<ProjectEntry>();

        try
        {
            var json = File.ReadAllText(_indexPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<ProjectEntry>>(json) ?? new List<ProjectEntry>();
        }
        catch
        {
            return new List<ProjectEntry>();
        }
    }

    private void WriteEntries(List<ProjectEntry> entries)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_indexPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = _indexPath + ".tmp";
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            File.Move(tempPath, _indexPath, overwrite: true);
        }
        catch
        {
            // Index freshness is helpful but must not break MCP tool execution.
        }
    }

    private static string? ReadOptionalString(JsonElement arguments, string propertyName)
        => arguments.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string NormalizePath(string path)
        => System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));

    private static string IdFromPath(string path)
        => $"path-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..10].ToLowerInvariant()}";

    private static string GetDefaultIndexPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appData, "VibeTracker", "projects.json");
    }
}
