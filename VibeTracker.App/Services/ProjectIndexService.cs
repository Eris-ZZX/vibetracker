using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VibeTracker.Core.Models;

namespace VibeTracker.App.Services;

/// <summary>
/// 项目索引管理：读写 %APPDATA%/VibeTracker/projects.json。
/// </summary>
public class ProjectIndexService
{
    private readonly string _indexPath;

    public ProjectIndexService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "VibeTracker");
        Directory.CreateDirectory(dir);
        _indexPath = Path.Combine(dir, "projects.json");
    }

    public List<ProjectEntry> Load()
    {
        if (!File.Exists(_indexPath))
            return new List<ProjectEntry>();

        try
        {
            var json = File.ReadAllText(_indexPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<ProjectEntry>>(json) ?? new List<ProjectEntry>();
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"[VibeTracker] 项目索引文件损坏 ({_indexPath})，返回空列表");
            return new List<ProjectEntry>();
        }
    }

    public void Save(List<ProjectEntry> projects)
    {
        var json = JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true });
        var dir = Path.GetDirectoryName(_indexPath)!;
        Directory.CreateDirectory(dir);

        var tempPath = _indexPath + ".tmp";
        File.WriteAllText(tempPath, json, new UTF8Encoding(false));
        File.Move(tempPath, _indexPath, overwrite: true);
    }

    public ProjectEntry? Find(string projectPath)
    {
        var projects = Load();
        return projects.FirstOrDefault(p =>
            string.Equals(p.Path, projectPath, StringComparison.OrdinalIgnoreCase));
    }

    public ProjectEntry Add(string name, string path, string tag, List<string> agents)
    {
        var projects = Load();

        // 避免重复
        var existing = projects.FirstOrDefault(p =>
            string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var entry = new ProjectEntry
        {
            Id = $"proj-{Guid.NewGuid():N}"[..10],
            Name = name,
            Path = path,
            Tag = tag,
            EnabledAgents = agents
        };

        projects.Add(entry);
        Save(projects);
        return entry;
    }

    public void Remove(string id)
    {
        var projects = Load();
        projects.RemoveAll(p => p.Id == id);
        Save(projects);
    }

    public void Update(string id, Action<ProjectEntry> update)
    {
        var projects = Load();
        var entry = projects.FirstOrDefault(p => p.Id == id);
        if (entry != null)
        {
            update(entry);
            Save(projects);
        }
    }

    public void UpdateLastActivity(string projectPath)
    {
        var projects = Load();
        var entry = projects.FirstOrDefault(p =>
            string.Equals(p.Path, projectPath, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            entry.LastActivityAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            Save(projects);
        }
    }

    /// <summary>
    /// 检查索引中所有项目路径是否存在，返回存在和不存在的列表。
    /// </summary>
    public (List<ProjectEntry> valid, List<ProjectEntry> invalid) Validate()
    {
        var projects = Load();
        var valid = new List<ProjectEntry>();
        var invalid = new List<ProjectEntry>();

        foreach (var p in projects)
        {
            if (Directory.Exists(p.Path))
                valid.Add(p);
            else
                invalid.Add(p);
        }

        return (valid, invalid);
    }
}
