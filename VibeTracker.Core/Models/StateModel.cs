using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VibeTracker.Core.Models;

public class FeatureItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "todo";
}

public class StateModel
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0.0";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("projectRoot")]
    public string ProjectRoot { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    [JsonPropertyName("lastSessionId")]
    public string LastSessionId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "unknown";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "in_progress";

    [JsonPropertyName("currentTask")]
    public string CurrentTask { get; set; } = string.Empty;

    [JsonPropertyName("features")]
    public List<FeatureItem> Features { get; set; } = new();

    [JsonPropertyName("completedSteps")]
    public List<string> CompletedSteps { get; set; } = new();

    [JsonPropertyName("inProgressSteps")]
    public List<string> InProgressSteps { get; set; } = new();

    [JsonPropertyName("pendingSteps")]
    public List<string> PendingSteps { get; set; } = new();

    [JsonPropertyName("blocker")]
    public string? Blocker { get; set; }

    [JsonPropertyName("lastAction")]
    public string LastAction { get; set; } = string.Empty;

    [JsonPropertyName("nextStep")]
    public string NextStep { get; set; } = string.Empty;
}
