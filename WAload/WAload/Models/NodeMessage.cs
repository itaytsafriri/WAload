using System.Text.Json.Serialization;

namespace WAload.Models
{
    public class NodeMessage
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("qr")]
        public string? Qr { get; set; }

        [JsonPropertyName("connected")]
        public bool? Connected { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("groups")]
        public List<GroupInfo>? Groups { get; set; }

        [JsonPropertyName("Media")]
        public MediaInfo? Media { get; set; }

        [JsonPropertyName("monitoring")]
        public bool? Monitoring { get; set; }
    }

    public class GroupInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class MediaInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("senderName")]
        public string? SenderName { get; set; }
    }
} 