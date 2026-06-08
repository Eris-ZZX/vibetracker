using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VibeTracker.Core.Models;

public class ProjectEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = $"proj-{Guid.NewGuid():N}"[..10];

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "轻量应用";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    [JsonPropertyName("lastActivityAt")]
    public string LastActivityAt { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    [JsonPropertyName("enabledAgents")]
    public List<string> EnabledAgents { get; set; } = new();
}
