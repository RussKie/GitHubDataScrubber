using System.Text.Json.Serialization;

public class Comment
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("parentId")]
    public string ParentId { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}
