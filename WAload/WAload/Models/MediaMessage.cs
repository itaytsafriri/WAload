using System;

namespace WAload.Models
{
    public class MediaMessage
    {
        public string Id { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public long Size { get; set; }
        public string SenderName { get; set; } = string.Empty;

        // Enhanced properties for TeleMedia features
        public string Body { get; set; } = string.Empty;
        public bool FromMe { get; set; } = false;
        public bool HasMedia { get; set; } = false;
        public string MediaType { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        
        // Convenience properties for compatibility
        public string FileName => Filename;
        public long FileSize => Size;

        public DateTime DateTime => DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;
    }
} 