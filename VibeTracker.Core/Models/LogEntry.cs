using System.Text.Json.Serialization;

namespace VibeTracker.Core.Models;

public enum LogType
{
    [JsonPropertyName("action")] Action,
    [JsonPropertyName("decision")] Decision,
    [JsonPropertyName("problem")] Problem,
    [JsonPropertyName("next")] Next
}

public class LogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "unknown";

    [JsonPropertyName("time")]
    public string Time { get; set; } = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "action";

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    [JsonPropertyName("cause")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cause { get; set; }

    [JsonPropertyName("resolution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Resolution { get; set; }

    [JsonPropertyName("resolved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Resolved { get; set; }
}
