using System;
using System.IO;
using System.Collections.Generic;
using WAload.Models;

namespace WAload.Services
{
    /// <summary>
    /// ch13 features - Service for managing folder structure including dated folders and format sorting
    /// </summary>
    public class FolderStructureService
    {
        private readonly AppSettings _settings;
        private DateTime _lastDateCheck;
        private System.Threading.Timer? _midnightTimer;

        public FolderStructureService(AppSettings settings)
        {
            _settings = settings;
            _lastDateCheck = DateTime.Now.Date;
            SetupMidnightTimer();
        }

        /// <summary>
        /// ch13 feature - Setup timer to check for midnight date changes
        /// </summary>
        private void SetupMidnightTimer()
        {
            // Calculate time until next midnight
            var now = DateTime.Now;
            var tomorrow = now.Date.AddDays(1);
            var timeUntilMidnight = tomorrow - now;

            _midnightTimer = new System.Threading.Timer(OnMidnightCheck, null, timeUntilMidnight, TimeSpan.FromDays(1));
        }

        /// <summary>
        /// ch13 feature - Check if date has changed and trigger folder creation if needed
        /// </summary>
        private void OnMidnightCheck(object? state)
        {
            var currentDate = DateTime.Now.Date;
            if (currentDate != _lastDateCheck)
            {
                _lastDateCheck = currentDate;
                System.Diagnostics.Debug.WriteLine($"[ch13] Date changed detected: {currentDate:dd-MM-yy}");
            }
        }

        /// <summary>
        /// ch13 feature - Get the appropriate target directory for file based on settings
        /// </summary>
        /// <param name="baseDownloadFolder">Base download folder</param>
        /// <param name="fileName">Original filename</param>
        /// <param name="mediaType">Type of media (image, video, etc.)</param>
        /// <returns>Target directory path where file should be saved</returns>
        public string GetTargetDirectory(string baseDownloadFolder, string fileName, string mediaType)
        {
            var targetDir = baseDownloadFolder;

            // ch13 feature - Apply dated folders if enabled
            if (_settings.DatedFolders)
            {
                var dateFolder = DateTime.Now.ToString("dd-MM-yy");
                targetDir = Path.Combine(targetDir, dateFolder);
                
                // Create date folder if it doesn't exist
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    System.Diagnostics.Debug.WriteLine($"[ch13] Created date folder: {targetDir}");
                }
            }

            // ch13 feature - Apply format sorting if enabled
            if (_settings.FolderFormatSorting)
            {
                var formatFolder = GetFormatFolder(fileName, mediaType);
                if (!string.IsNullOrEmpty(formatFolder))
                {
                    targetDir = Path.Combine(targetDir, formatFolder);
                    
                    // Create format folder if it doesn't exist
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        System.Diagnostics.Debug.WriteLine($"[ch13] Created format folder: {targetDir}");
                    }
                }
            }

            return targetDir;
        }

        /// <summary>
        /// ch13 feature - Determine format folder based on file extension and media type
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="mediaType">Media type from WhatsApp</param>
        /// <returns>Format folder name</returns>
        private string GetFormatFolder(string fileName, string mediaType)
        {
            // First check file extension
            var extension = Path.GetExtension(fileName).ToLower();
            
            // Image formats
            var imageExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".svg" };
            if (imageExtensions.Contains(extension))
            {
                return "image";
            }
            
            // Video formats
            var videoExtensions = new HashSet<string> { ".mp4", ".avi", ".mov", ".webm", ".mkv", ".wmv", ".flv", ".m4v" };
            if (videoExtensions.Contains(extension))
            {
                return "mp4";
            }
            
            // MXF format (processed files)
            if (extension == ".mxf")
            {
                return "mxf";
            }
            
            // Fallback to media type if extension doesn't match
            return mediaType?.ToLower() switch
            {
                "image" => "image",
                "video" => "mp4",
                _ => "other"
            };
        }

        /// <summary>
        /// ch13 feature - Check and create date folder if needed (called on app load)
        /// </summary>
        /// <param name="baseDownloadFolder">Base download folder</param>
        public void EnsureDateFolderExists(string baseDownloadFolder)
        {
            if (_settings.DatedFolders)
            {
                var dateFolder = DateTime.Now.ToString("dd-MM-yy");
                var dateFolderPath = Path.Combine(baseDownloadFolder, dateFolder);
                
                if (!Directory.Exists(dateFolderPath))
                {
                    Directory.CreateDirectory(dateFolderPath);
                    System.Diagnostics.Debug.WriteLine($"[ch13] Created date folder on load: {dateFolderPath}");
                }
            }
        }

        /// <summary>
        /// ch13 feature - Ensure format folders exist (called when format sorting is enabled)
        /// </summary>
        /// <param name="baseDirectory">Base directory where format folders should be created</param>
        public void EnsureFormatFoldersExist(string baseDirectory)
        {
            if (_settings.FolderFormatSorting)
            {
                var formatFolders = new[] { "image", "mp4", "mxf" };
                
                foreach (var folder in formatFolders)
                {
                    var folderPath = Path.Combine(baseDirectory, folder);
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                        System.Diagnostics.Debug.WriteLine($"[ch13] Created format folder: {folderPath}");
                    }
                }
            }
        }

        /// <summary>
        /// ch13 feature - Get the target directory for processed files (like MXF)
        /// Takes into account both original file location and processing output
        /// </summary>
        /// <param name="originalFilePath">Path of the original file</param>
        /// <param name="outputExtension">Extension of the processed file (e.g., ".mxf")</param>
        /// <returns>Target directory for processed file</returns>
        public string GetProcessedFileTargetDirectory(string originalFilePath, string outputExtension)
        {
            var originalDir = Path.GetDirectoryName(originalFilePath) ?? string.Empty;
            
            // If format sorting is enabled, determine target based on original and output file types
            if (_settings.FolderFormatSorting)
            {
                // Get the original file extension to understand what type it was
                var originalExtension = Path.GetExtension(originalFilePath).ToLower();
                var originalFormat = GetFormatFolder(Path.GetFileName(originalFilePath), "");
                
                System.Diagnostics.Debug.WriteLine($"[ch13] Processing target directory - Original: {originalExtension} ({originalFormat}), Output: {outputExtension}");
                
                // Determine target format folder based on original file type, not output extension
                // This keeps processed images in image folder, processed videos in mp4 folder unless converting to MXF
                string targetFormatFolder;
                
                if (originalFormat == "image")
                {
                    // Keep all processed images in the image folder regardless of output format
                    targetFormatFolder = "image";
                    System.Diagnostics.Debug.WriteLine($"[ch13] Original was image, keeping processed file in image folder");
                }
                else if (originalFormat == "mp4" && outputExtension.ToLower() == ".mxf")
                {
                    // Video converted to MXF goes to MXF folder
                    targetFormatFolder = "mxf";
                    System.Diagnostics.Debug.WriteLine($"[ch13] Video converted to MXF, moving to mxf folder");
                }
                else if (originalFormat == "mp4")
                {
                    // Processed video stays in mp4 folder
                    targetFormatFolder = "mp4";
                    System.Diagnostics.Debug.WriteLine($"[ch13] Video processed, staying in mp4 folder");
                }
                else
                {
                    // For other types, use output extension
                    targetFormatFolder = outputExtension.ToLower() switch
                    {
                        ".mxf" => "mxf",
                        ".mp4" => "mp4", 
                        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".tiff" or ".svg" => "image",
                        _ => "other"
                    };
                    System.Diagnostics.Debug.WriteLine($"[ch13] Other file type, using output extension logic: {targetFormatFolder}");
                }
                
                // Check if we're already in the target format folder
                var currentFolderName = Path.GetFileName(originalDir).ToLower();
                if (currentFolderName == targetFormatFolder)
                {
                    // Already in the correct folder
                    System.Diagnostics.Debug.WriteLine($"[ch13] Already in correct folder: {originalDir}");
                    return originalDir;
                }
                
                // Need to move to target format folder
                string targetDir;
                
                // If we're in a format folder, go up one level then into target folder
                if (currentFolderName == "image" || currentFolderName == "mp4" || currentFolderName == "mxf")
                {
                    var parentDir = Directory.GetParent(originalDir)?.FullName ?? originalDir;
                    targetDir = Path.Combine(parentDir, targetFormatFolder);
                    System.Diagnostics.Debug.WriteLine($"[ch13] Moving from format folder {currentFolderName} to {targetFormatFolder}");
                }
                else
                {
                    // We're in base directory, create subfolder
                    targetDir = Path.Combine(originalDir, targetFormatFolder);
                    System.Diagnostics.Debug.WriteLine($"[ch13] Creating subfolder {targetFormatFolder} in {originalDir}");
                }
                
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    System.Diagnostics.Debug.WriteLine($"[ch13] Created processed file format folder: {targetDir}");
                }
                
                return targetDir;
            }
            
            // If format sorting is disabled, keep in same directory as original
            return originalDir;
        }

        public void Dispose()
        {
            _midnightTimer?.Dispose();
        }
    }
}
