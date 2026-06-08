using System;

namespace VibeTracker.Core;

/// <summary>
/// 简易唯一 ID 生成器：时间戳前缀 + 短 GUID，可排序且无碰撞。
/// </summary>
public static class IdGenerator
{
    public static string NewId()
    {
        // 格式: yyyyMMddHHmmss-xxxx (25 字符，时间可排序)
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var rnd = Guid.NewGuid().ToString("N")[..8];
        return $"{ts}-{rnd}";
    }
}
