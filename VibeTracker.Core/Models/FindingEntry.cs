using System.Text.Json.Serialization;

namespace VibeTracker.Core.Models;

public enum FindingType
{
    [JsonPropertyName("good")] Good,
    [JsonPropertyName("pit")] Pit
}

public class FindingEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "unknown";

    [JsonPropertyName("time")]
    public string Time { get; set; } = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

    [JsonPropertyName("type")]
    public string Type { get; set; } = "pit";

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("consequence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Consequence { get; set; }
}
