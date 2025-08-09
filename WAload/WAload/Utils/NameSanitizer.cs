using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WAload.Utils
{
    public static class NameSanitizer
    {
        public static string? ExtractNameFromMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int idx = text.IndexOf("_name", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            string rest = text.Substring(idx + "_name".Length).Trim();
            // Remove common separators
            rest = rest.TrimStart(':', '-', ' ', '_');
            if (string.IsNullOrWhiteSpace(rest)) return null;
            var token = rest.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        public static string NormalizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            // Remove underscores as requested
            var s = name.Replace("_", " ");
            // Remove invalid Windows path chars
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "");
            // Collapse whitespace
            s = Regex.Replace(s, @"\s+", " ").Trim();
            // Remove potential path separators
            s = s.Replace("/", "").Replace("\\", "");
            return s;
        }

        public static bool IsHebrew(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Any(ch => ch >= '\u0590' && ch <= '\u05FF');
        }
    }
}

