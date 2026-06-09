using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace VibeTracker.Core;

/// <summary>
/// 文件读写引擎：原子写、追加写、备份、项目级 Mutex。
/// </summary>
public class FileEngine
{
    private readonly string _projectRoot;
    private readonly string _vibeDir;
    private readonly string _bakDir;
    private readonly string _vibeProjectHash;
    private readonly int _maxBackups = 10;

    public FileEngine(string projectRoot)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _vibeDir = Path.Combine(_projectRoot, ".vibe");
        _bakDir = Path.Combine(_vibeDir, ".bak");

        // 用项目路径的 hash 生成稳定的 Mutex 名称
        _vibeProjectHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(_projectRoot)))[..16];
    }

    public string VibeDir => _vibeDir;
    public string ProjectRoot => _projectRoot;

    // ─────── 目录 ───────

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(_vibeDir);
        Directory.CreateDirectory(_bakDir);
    }

    public bool Exists() => Directory.Exists(_vibeDir) &&
                            File.Exists(Path.Combine(_vibeDir, "config.json"));

    public bool VibeDirExists() => Directory.Exists(_vibeDir);

    // ─────── Mutex ───────

    private Mutex? AcquireMutex(int timeoutMs = 5000)
    {
        var mutex = new Mutex(initiallyOwned: false, $@"Local\VibeTracker-{_vibeProjectHash}");
        try
        {
            if (mutex.WaitOne(timeoutMs))
                return mutex;
        }
        catch (AbandonedMutexException)
        {
            // 前一个持有者崩溃了，锁已被释放，当前线程已持有
            return mutex;
        }

        mutex.Dispose();
        return null;
    }

    public T WithLock<T>(Func<T> action, int timeoutMs = 5000)
    {
        var mutex = AcquireMutex(timeoutMs);
        if (mutex == null)
            throw new TimeoutException("无法获取项目写入锁，可能其他进程正在操作同一项目。");

        try
        {
            return action();
        }
        finally
        {
            try { mutex.ReleaseMutex(); }
            finally { mutex.Dispose(); }
        }
    }

    public void WithLock(Action action, int timeoutMs = 5000)
    {
        WithLock<object?>(() =>
        {
            action();
            return null;
        }, timeoutMs);
    }

    // ─────── 原子覆盖写（state.json / config.json） ───────

    public void AtomicWriteJson<T>(string relativePath, T data)
    {
        var filePath = Path.Combine(_vibeDir, relativePath);
        var tempPath = filePath + ".tmp";

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // 刷新到磁盘
        using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fs.Flush(true); // flush OS buffers
        }

        // 原子 rename
        File.Move(tempPath, filePath, overwrite: true);
    }

    // ─────── 读 JSON ───────

    public T ReadJson<T>(string relativePath) where T : new()
    {
        var filePath = Path.Combine(_vibeDir, relativePath);

        if (!File.Exists(filePath))
            return new T();

        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        catch (JsonException)
        {
            // 文件损坏 → 尝试从最新备份恢复
            var latestBak = GetLatestBackup(relativePath);
            if (latestBak != null)
            {
                try
                {
                    var bakJson = File.ReadAllText(latestBak, Encoding.UTF8);
                    var data = JsonSerializer.Deserialize<T>(bakJson);
                    if (data != null)
                    {
                        // 恢复成功：写回主文件
                        WithLock(() => AtomicWriteJson(relativePath, data));
                        return data;
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine($"[VibeTracker] 备份恢复失败 {relativePath}: {ex.Message}"); }
            }

            // 无法恢复 → 返回默认值，调用方自行处理
            return new T();
        }
    }

    public (T? Data, string? Error) TryReadJson<T>(string relativePath) where T : class
    {
        var filePath = Path.Combine(_vibeDir, relativePath);

        if (!File.Exists(filePath))
            return (null, $"文件不存在: {relativePath}");

        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<T>(json);
            return (data, null);
        }
        catch (JsonException ex)
        {
            return (null, $"JSON 解析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 尝试从最新备份恢复 JSON 文件。仅在 ReadJson/TryReadJson 发现损坏时调用。
    /// </summary>
    public T? TryRecoverJson<T>(string relativePath) where T : class
    {
        var latestBak = GetLatestBackup(relativePath);
        if (latestBak == null)
            return null;

        try
        {
            var json = File.ReadAllText(latestBak, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<T>(json);
            if (data != null)
            {
                WithLock(() => AtomicWriteJson(relativePath, data));
                return data;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 读 JSONL 文件，返回解析信息（含跳过的损坏行数）。
    /// </summary>
    public (List<T> Data, int CorruptedLines) ReadJsonLinesWithStats<T>(string relativePath) where T : new()
    {
        var filePath = Path.Combine(_vibeDir, relativePath);
        var results = new List<T>();
        var corrupted = 0;

        if (!File.Exists(filePath))
            return (results, 0);

        foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<T>(line);
                if (entry != null) results.Add(entry);
            }
            catch (JsonException) { corrupted++; }
        }

        return (results, corrupted);
    }

    // ─────── 追加写 JSONL ───────

    public string AppendJsonLine<T>(string relativePath, T entry, string? id = null)
    {
        var filePath = Path.Combine(_vibeDir, relativePath);

        // 用 ULID 生成 ID（时间排序 + 单调递增）
        var entryId = id ?? IdGenerator.NewId();

        // 如果模型有 Id 属性就自动填入
        var idProp = typeof(T).GetProperty("Id");
        if (idProp != null && idProp.PropertyType == typeof(string) && idProp.CanWrite)
        {
            idProp.SetValue(entry, entryId);
        }

        var jsonLine = JsonSerializer.Serialize(entry, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var line = jsonLine + "\n";
        using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            fs.Write(Encoding.UTF8.GetBytes(line));
            fs.Flush(true);
        }

        return entryId;
    }

    // ─────── 读 JSONL ───────

    public List<T> ReadJsonLines<T>(string relativePath, int takeLast = 0) where T : new()
    {
        var filePath = Path.Combine(_vibeDir, relativePath);
        var results = new List<T>();

        if (!File.Exists(filePath))
            return results;

        foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<T>(line);
                if (entry != null)
                    results.Add(entry);
            }
            catch (JsonException)
            {
                // 跳过损坏的行
            }
        }

        if (takeLast > 0 && results.Count > takeLast)
            return results.GetRange(results.Count - takeLast, takeLast);

        return results;
    }

    public List<T> ReadJsonLinesReverse<T>(string relativePath, int take) where T : new()
    {
        // 从末尾倒序读 N 行（用于 get_context 取最近日志）
        var filePath = Path.Combine(_vibeDir, relativePath);
        var results = new List<T>();

        if (!File.Exists(filePath))
            return results;

        // 小文件直接全读
        var allLines = File.ReadAllLines(filePath, Encoding.UTF8);
        for (int i = allLines.Length - 1; i >= 0 && results.Count < take; i--)
        {
            if (string.IsNullOrWhiteSpace(allLines[i]))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<T>(allLines[i]);
                if (entry != null)
                    results.Add(entry);
            }
            catch (JsonException) { }
        }

        results.Reverse();
        return results;
    }

    // ─────── Markdown 文件 ───────

    public string ReadMarkdown(string relativePath)
    {
        var filePath = Path.Combine(_vibeDir, relativePath);
        return File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.UTF8) : string.Empty;
    }

    public void WriteMarkdown(string relativePath, string content)
    {
        var filePath = Path.Combine(_vibeDir, relativePath);
        var tempPath = filePath + ".tmp";

        File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            fs.Flush(true);

        File.Move(tempPath, filePath, overwrite: true);
    }

    // ─────── 备份 ───────

    public void Backup(string relativePath)
    {
        var filePath = Path.Combine(_vibeDir, relativePath);
        if (!File.Exists(filePath))
            return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var bakPath = Path.Combine(_bakDir, $"{Path.GetFileNameWithoutExtension(relativePath)}.{timestamp}{Path.GetExtension(relativePath)}");

        File.Copy(filePath, bakPath, overwrite: true);

        // 清理旧备份：保留最近 N 个
        PruneBackups(relativePath);
    }

    private void PruneBackups(string relativePath)
    {
        var prefix = Path.GetFileNameWithoutExtension(relativePath);
        var ext = Path.GetExtension(relativePath);

        var backups = Directory.GetFiles(_bakDir, $"{prefix}.*{ext}");
        Array.Sort(backups); // 按文件名（含时间戳）排序

        if (backups.Length > _maxBackups)
        {
            for (int i = 0; i < backups.Length - _maxBackups; i++)
            {
                try { File.Delete(backups[i]); }
                catch { /* 忽略删除失败 */ }
            }
        }
    }

    public string? GetLatestBackup(string relativePath)
    {
        if (!Directory.Exists(_bakDir))
            return null;

        var prefix = Path.GetFileNameWithoutExtension(relativePath);
        var ext = Path.GetExtension(relativePath);

        var backups = Directory.GetFiles(_bakDir, $"{prefix}.*{ext}");
        Array.Sort(backups);

        return backups.Length > 0 ? backups[^1] : null;
    }

    // ─────── 文件存在检查 ───────

    public bool FileExists(string relativePath)
        => File.Exists(Path.Combine(_vibeDir, relativePath));
}
