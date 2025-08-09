using System;
using System.IO;
using WAload.Utils;

namespace WAload.Services
{
    public static class FolderSortingService
    {
        public static string EnsurePerNameFolder(string? dlRoot, string messageBody, string senderName)
        {
            var nameFromMsg = NameSanitizer.ExtractNameFromMessage(messageBody) ?? senderName;
            var sanitized = NameSanitizer.NormalizeFolderName(nameFromMsg);
            var root = string.IsNullOrWhiteSpace(dlRoot) ? Path.Combine(AppContext.BaseDirectory, "downloads") : dlRoot;
            var perNameFolder = Path.Combine(root, sanitized);
            Directory.CreateDirectory(perNameFolder);
            return perNameFolder;
        }

        public static (string origPath, string procPath) GetOrigAndProcPaths(string perNameFolder, string extension)
        {
            var ext = string.IsNullOrEmpty(extension) ? "" : extension;
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var origName = $"orig_{ts}{ext}";
            var procName = $"proc_{ts}{ext}";
            var origPath = Path.Combine(perNameFolder, origName);
            var procPath = Path.Combine(perNameFolder, procName);
            return (origPath, procPath);
        }
    }
}

