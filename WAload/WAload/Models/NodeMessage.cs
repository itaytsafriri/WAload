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

        [JsonPropertyName("Text")]
        public TextInfo? Text { get; set; }

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
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("From")]
        public string? From { get; set; }

        [JsonPropertyName("Author")]
        public string? Author { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("Filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("Data")]
        public string? Data { get; set; }

        [JsonPropertyName("Size")]
        public long? Size { get; set; }

        [JsonPropertyName("SenderName")]
        public string? SenderName { get; set; }
    }

    public class TextInfo
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("From")]
        public string? From { get; set; }

        [JsonPropertyName("Author")]
        public string? Author { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("SenderName")]
        public string? SenderName { get; set; }
    }
} 