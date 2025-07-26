using System.Text.Json.Serialization;

namespace WAload.Models
{
    public class TextMessage
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