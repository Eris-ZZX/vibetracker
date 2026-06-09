using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VibeTracker.Core.Models;

public class ConfigModel
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("seed")]
    public string Seed { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new() { "@frontend", "@backend", "@bug", "@config", "@general" };

    [JsonPropertyName("agentPreferences")]
    public AgentPreferences AgentPreferences { get; set; } = new();
}

public class AgentPreferences
{
    [JsonPropertyName("contextLines")]
    public int ContextLines { get; set; } = 5;

    [JsonPropertyName("autoCheckConsistency")]
    public bool AutoCheckConsistency { get; set; } = true;
}
