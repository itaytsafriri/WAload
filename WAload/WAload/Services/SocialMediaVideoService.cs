using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Aggregate

namespace WAload.Services
{
    public class TimeSegment
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        public TimeSegment(TimeSpan start, TimeSpan end)
        {
            StartTime = start;
            EndTime = end;
        }
        
        public override string ToString()
        {
            // Format for yt-dlp --download-sections: "*00:01:30-00:02:00"
            return $"*{StartTime:hh\\:mm\\:ss}-{EndTime:hh\\:mm\\:ss}";
        }
    }

    public class SocialMediaVideoService
    {
        private readonly string _ytdlpPath;
        private readonly string _downloadFolder;
        private readonly SupabaseLoggingService _loggingService;

        public SocialMediaVideoService(string downloadFolder)
        {
            // Get the directory where the current assembly is located
            var assemblyDirectory = AppContext.BaseDirectory;
            
            // Construct path to yt-dlp executable
            _ytdlpPath = Path.Combine(assemblyDirectory, "ytdl", "yt-dlp.exe");
            _downloadFolder = downloadFolder;
            
            // Initialize logging service
            _loggingService = new SupabaseLoggingService();
            
            System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Looking for yt-dlp at: {_ytdlpPath}");
            System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Assembly directory: {assemblyDirectory}");
            
            if (!File.Exists(_ytdlpPath))
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Warning: yt-dlp not found at {_ytdlpPath}");
                
                // Try alternative paths for debug mode
                var alternativePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "ytdl", "yt-dlp.exe"),
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "ytdl", "yt-dlp.exe"),
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "..", "ytdl", "yt-dlp.exe")
                };
                
                foreach (var altPath in alternativePaths)
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Trying alternative path: {altPath}");
                    if (File.Exists(altPath))
                    {
                        _ytdlpPath = altPath;
                        System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Found yt-dlp at alternative path: {_ytdlpPath}");
                        break;
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp found at: {_ytdlpPath}");
            }
        }

        /// <summary>
        /// Parses time segments from a message using various forgiving syntax formats
        /// </summary>
        /// <param name="message">The message containing time segment information</param>
        /// <returns>List of TimeSegment objects, or empty list if no valid segments found</returns>
        public List<TimeSegment> ParseTimeSegments(string message)
        {
            var segments = new List<TimeSegment>();
            
            if (string.IsNullOrEmpty(message))
                return segments;

            // Multiple regex patterns to catch various syntax formats
            var patterns = new[]
            {
                // Standard format: (IN:00:01:00 OUT:00:02:00) - case insensitive
                @"\([Ii][Nn]:\s*(\d{1,2}):(\d{1,2}):(\d{1,2})\s+[Oo][Uu][Tt]:\s*(\d{1,2}):(\d{1,2}):(\d{1,2})\)",
                
                // Alternative format: [IN:00:01:00 OUT:00:02:00] - case insensitive
                @"\[[Ii][Nn]:\s*(\d{1,2}):(\d{1,2}):(\d{1,2})\s+[Oo][Uu][Tt]:\s*(\d{1,2}):(\d{1,2}):(\d{1,2})\]",
                
                // Short format with IN/OUT: (in:01:10 out:02:00) - case insensitive, minutes:seconds
                @"\([Ii][Nn]:\s*(\d{1,2}):(\d{1,2})\s+[Oo][Uu][Tt]:\s*(\d{1,2}):(\d{1,2})\)",
                
                // Simple format: (00:01:00-00:02:00)
                @"\((\d{1,2}):(\d{1,2}):(\d{1,2})\s*-\s*(\d{1,2}):(\d{1,2}):(\d{1,2})\)",
                
                // Without brackets: 00:01:00-00:02:00
                @"(\d{1,2}):(\d{1,2}):(\d{1,2})\s*-\s*(\d{1,2}):(\d{1,2}):(\d{1,2})",
                
                // With "to" instead of "-": 00:01:00 to 00:02:00
                @"(\d{1,2}):(\d{1,2}):(\d{1,2})\s+to\s+(\d{1,2}):(\d{1,2}):(\d{1,2})",
                
                // With "until" instead of "-": 00:01:00 until 00:02:00
                @"(\d{1,2}):(\d{1,2}):(\d{1,2})\s+until\s+(\d{1,2}):(\d{1,2}):(\d{1,2})",
                
                // Short format: 1:30-2:45 (assumes minutes:seconds)
                @"(\d{1,2}):(\d{1,2})\s*-\s*(\d{1,2}):(\d{1,2})",
                
                // Very short format: 1:30 to 2:45 (assumes minutes:seconds)
                @"(\d{1,2}):(\d{1,2})\s+to\s+(\d{1,2}):(\d{1,2})"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(message, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    try
                    {
                        TimeSpan startTime, endTime;
                        
                        if (match.Groups.Count == 7) // Full format with hours:minutes:seconds
                        {
                            var startHours = int.Parse(match.Groups[1].Value);
                            var startMinutes = int.Parse(match.Groups[2].Value);
                            var startSeconds = int.Parse(match.Groups[3].Value);
                            var endHours = int.Parse(match.Groups[4].Value);
                            var endMinutes = int.Parse(match.Groups[5].Value);
                            var endSeconds = int.Parse(match.Groups[6].Value);
                            
                            startTime = new TimeSpan(startHours, startMinutes, startSeconds);
                            endTime = new TimeSpan(endHours, endMinutes, endSeconds);
                        }
                        else if (match.Groups.Count == 5) // Short format with minutes:seconds (including IN/OUT format)
                        {
                            var startMinutes = int.Parse(match.Groups[1].Value);
                            var startSeconds = int.Parse(match.Groups[2].Value);
                            var endMinutes = int.Parse(match.Groups[3].Value);
                            var endSeconds = int.Parse(match.Groups[4].Value);
                            
                            startTime = new TimeSpan(0, startMinutes, startSeconds);
                            endTime = new TimeSpan(0, endMinutes, endSeconds);
                        }
                        else
                        {
                            continue; // Skip invalid matches
                        }
                        
                        // Validate times
                        if (startTime >= endTime)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Invalid time segment: start ({startTime}) >= end ({endTime})");
                            continue;
                        }
                        
                        // Check for reasonable duration (max 10 hours)
                        var duration = endTime - startTime;
                        if (duration.TotalHours > 10)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Time segment too long: {duration.TotalHours:F2} hours");
                            continue;
                        }
                        
                        segments.Add(new TimeSegment(startTime, endTime));
                        System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Parsed time segment: {startTime:hh\\:mm\\:ss} to {endTime:hh\\:mm\\:ss}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Error parsing time segment: {ex.Message}");
                        continue;
                    }
                }
            }
            
            return segments;
        }

        /// <summary>
        /// Checks if a message contains social media video links
        /// </summary>
        /// <param name="message">The message text to check</param>
        /// <returns>True if the message contains social media video links</returns>
        public bool ContainsSocialMediaVideoLinks(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // Patterns for social media video platforms
            var patterns = new[]
            {
                @"(?:https?://)?(?:www\.)?(?:youtube\.com|youtu\.be)/[^\s]+", // YouTube
                @"(?:https?://)?(?:www\.)?(?:twitter\.com|x\.com)/[^\s]+", // X/Twitter
                @"(?:https?://)?(?:www\.)?(?:facebook\.com|fb\.com)/[^\s]+", // Facebook
                @"(?:https?://)?(?:www\.)?(?:tiktok\.com)/[^\s]+", // TikTok
                @"(?:https?://)?(?:www\.)?(?:instagram\.com)/[^\s]+" // Instagram
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts social media video links from a message
        /// </summary>
        /// <param name="message">The message text</param>
        /// <returns>Array of video URLs found in the message</returns>
        public string[] ExtractSocialMediaVideoLinks(string message)
        {
            if (string.IsNullOrEmpty(message))
                return Array.Empty<string>();

            var patterns = new[]
            {
                @"(?:https?://)?(?:www\.)?(?:youtube\.com|youtu\.be)/[^\s]+", // YouTube
                @"(?:https?://)?(?:www\.)?(?:twitter\.com|x\.com)/[^\s]+", // X/Twitter
                @"(?:https?://)?(?:www\.)?(?:facebook\.com|fb\.com)/[^\s]+", // Facebook
                @"(?:https?://)?(?:www\.)?(?:tiktok\.com)/[^\s]+", // TikTok
                @"(?:https?://)?(?:www\.)?(?:instagram\.com)/[^\s]+" // Instagram
            };

            var links = new List<string>();

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(message, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var url = match.Value;
                    // Ensure URL has protocol
                    if (!url.StartsWith("http"))
                    {
                        url = "https://" + url;
                    }
                    links.Add(url);
                }
            }

            return links.ToArray();
        }

        /// <summary>
        /// Downloads a social media video using yt-dlp with optional time segment extraction
        /// </summary>
        /// <param name="url">The video URL to download</param>
        /// <param name="senderName">Name of the sender for file naming</param>
        /// <param name="timestamp">Timestamp for file naming</param>
        /// <param name="timeSegments">Optional time segments to extract</param>
        /// <returns>The path to the downloaded video file, or null if failed</returns>
        public async Task<string?> DownloadSocialMediaVideoAsync(string url, string senderName, DateTime timestamp, List<TimeSegment>? timeSegments = null)
        {
            try
            {
                if (!File.Exists(_ytdlpPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp is not available. Skipping download.");
                    return null;
                }

                // Create safe filename
                var safeSenderName = Path.GetInvalidFileNameChars().Aggregate(senderName, (current, c) => current.Replace(c, '_'));
                var timestampStr = timestamp.ToString("yyyyMMdd_HHmmss");
                var outputTemplate = Path.Combine(_downloadFolder, $"{safeSenderName}_{timestampStr}.%(ext)s");

                // Build yt-dlp arguments
                var arguments = $"-o \"{outputTemplate}\" --no-playlist --no-check-certificate";
                
                // Add time segment extraction if specified
                if (timeSegments != null && timeSegments.Count > 0)
                {
                    var segmentArgs = string.Join(" ", timeSegments.Select(s => s.ToString()));
                    arguments += $" --download-sections \"{segmentArgs}\"";
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Adding time segments: {segmentArgs}");
                }
                
                arguments += $" \"{url}\"";

                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Downloading video from: {url}");
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp command: {arguments}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytdlpPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_ytdlpPath)
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp error: {error}");
                }

                if (process.ExitCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp failed with exit code: {process.ExitCode}");
                    
                    // Log failed ytdl download
                    await LogYtdlDownload(url, senderName, false, "", $"yt-dlp failed with exit code {process.ExitCode}: {error}");
                    
                    return null;
                }

                // Wait a moment for the file to be fully written
                await Task.Delay(1000);

                // Find the downloaded file
                var downloadedFile = FindDownloadedFile(safeSenderName, timestampStr);
                if (downloadedFile != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Successfully downloaded: {downloadedFile}");
                    
                    // Log successful ytdl download
                    await LogYtdlDownload(url, senderName, true, Path.GetExtension(downloadedFile), null);
                    
                    return downloadedFile;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Could not find downloaded file");
                    
                    // Log failed ytdl download
                    await LogYtdlDownload(url, senderName, false, "", "Downloaded file not found");
                    
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Exception downloading video: {ex.Message}");
                
                // Log exception in ytdl download
                await LogYtdlDownload(url, senderName, false, "", ex.Message);
                
                return null;
            }
        }

        /// <summary>
        /// Logs ytdl download results to Supabase
        /// </summary>
        private async Task LogYtdlDownload(string url, string senderName, bool successful, string extension, string? errors)
        {
            try
            {
                // Determine link type from URL
                var linkType = "other";
                var urlLower = url.ToLower();
                
                if (urlLower.Contains("youtube.com") || urlLower.Contains("youtu.be"))
                    linkType = "youtube";
                else if (urlLower.Contains("twitter.com") || urlLower.Contains("x.com"))
                    linkType = "twitter";
                else if (urlLower.Contains("tiktok.com"))
                    linkType = "tiktok";
                else if (urlLower.Contains("instagram.com"))
                    linkType = "instagram";
                else if (urlLower.Contains("facebook.com"))
                    linkType = "facebook";
                
                await _loggingService.LogMediaProcessingAsync(
                    senderName, 
                    "video", 
                    false, // autoConverted - not applicable for downloads
                    successful, 
                    extension,
                    true, // isLink - always true for social media downloads
                    linkType,
                    true, // ytdlUsed - always true for this service
                    errors
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Failed to log to Supabase: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads a social media video with automatic time segment parsing from the message
        /// </summary>
        /// <param name="url">The video URL to download</param>
        /// <param name="senderName">Name of the sender for file naming</param>
        /// <param name="timestamp">Timestamp for file naming</param>
        /// <param name="message">The original message containing time segment information</param>
        /// <returns>The path to the downloaded video file, or null if failed</returns>
        public async Task<string?> DownloadSocialMediaVideoWithSegmentsAsync(string url, string senderName, DateTime timestamp, string message)
        {
            try
            {
                // Parse time segments from the message
                var timeSegments = ParseTimeSegments(message);
                
                if (timeSegments.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Found {timeSegments.Count} time segment(s) in message");
                    foreach (var segment in timeSegments)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Segment: {segment.StartTime:hh\\:mm\\:ss} to {segment.EndTime:hh\\:mm\\:ss}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] No time segments found in message, downloading full video");
                }
                
                // Download with segments
                return await DownloadSocialMediaVideoAsync(url, senderName, timestamp, timeSegments);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Exception in DownloadSocialMediaVideoWithSegmentsAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validates time segment format and provides helpful feedback
        /// </summary>
        /// <param name="timeString">The time string to validate</param>
        /// <returns>Validation result with suggestions</returns>
        public (bool IsValid, string Message, TimeSegment? Segment) ValidateTimeSegment(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
            {
                return (false, "Time segment cannot be empty", null);
            }

            // Try to parse using our existing method
            var segments = ParseTimeSegments(timeString);
            
            if (segments.Count == 0)
            {
                return (false, 
                    "Invalid time format. Supported formats:\n" +
                    "• (IN:00:01:00 OUT:00:02:00)\n" +
                    "• (00:01:00-00:02:00)\n" +
                    "• 00:01:00-00:02:00\n" +
                    "• 00:01:00 to 00:02:00\n" +
                    "• 1:30-2:45 (minutes:seconds)", 
                    null);
            }
            
            if (segments.Count > 1)
            {
                return (false, $"Multiple time segments found ({segments.Count}). Please specify only one segment.", null);
            }
            
            return (true, "Valid time segment", segments[0]);
        }

        /// <summary>
        /// Gets a user-friendly description of supported time segment formats
        /// </summary>
        /// <returns>Formatted string with examples</returns>
        public string GetSupportedTimeFormats()
        {
            return @"Supported time segment formats:

1. Standard format: (IN:00:01:00 OUT:00:02:00)
2. Alternative brackets: [IN:00:01:00 OUT:00:02:00]
3. Simple format: (00:01:00-00:02:00)
4. No brackets: 00:01:00-00:02:00
5. Using 'to': 00:01:00 to 00:02:00
6. Using 'until': 00:01:00 until 00:02:00
7. Short format: 1:30-2:45 (assumes minutes:seconds)
8. Very short: 1:30 to 2:45 (assumes minutes:seconds)

Examples:
• https://youtube.com/watch?v=abc123 (IN:00:01:30 OUT:00:03:45)
• https://youtube.com/watch?v=abc123 (1:30-3:45)
• https://youtube.com/watch?v=abc123 00:01:30 to 00:03:45

Note: Requires ffmpeg to be in the same directory as yt-dlp for time segment extraction.";
        }

        /// <summary>
        /// Finds the downloaded file based on the naming pattern
        /// </summary>
        /// <param name="senderName">Sender name used in filename</param>
        /// <param name="timestamp">Timestamp used in filename</param>
        /// <returns>Path to the downloaded file, or null if not found</returns>
        private string? FindDownloadedFile(string senderName, string timestamp)
        {
            try
            {
                var pattern = $"{senderName}_{timestamp}.*";
                var files = Directory.GetFiles(_downloadFolder, pattern);

                if (files.Length > 0)
                {
                    // Filter out .json files and prefer video files
                    var videoFiles = files.Where(f => !f.EndsWith(".json") && !f.EndsWith(".part")).ToArray();
                    
                    if (videoFiles.Length > 0)
                    {
                        // Return the first video file matching the pattern
                        System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Found video file: {videoFiles[0]}");
                        return videoFiles[0];
                    }
                    else
                    {
                        // If no video files found, return the first non-json file
                        var nonJsonFiles = files.Where(f => !f.EndsWith(".json")).ToArray();
                        if (nonJsonFiles.Length > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Found non-json file: {nonJsonFiles[0]}");
                            return nonJsonFiles[0];
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] No downloaded file found for pattern: {pattern}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Exception finding downloaded file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if yt-dlp is available
        /// </summary>
        /// <returns>True if yt-dlp executable exists</returns>
        public bool IsYtDlpAvailable()
        {
            return File.Exists(_ytdlpPath);
        }

        /// <summary>
        /// Gets the yt-dlp version
        /// </summary>
        /// <returns>Version string or error message</returns>
        public async Task<string> GetYtDlpVersion()
        {
            try
            {
                if (!File.Exists(_ytdlpPath))
                {
                    return "yt-dlp not found";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytdlpPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_ytdlpPath)
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return output.Trim();
            }
            catch (Exception ex)
            {
                return $"Error getting version: {ex.Message}";
            }
        }

        /// <summary>
        /// Updates yt-dlp to the latest version
        /// </summary>
        /// <returns>Update result message</returns>
        public async Task<string> UpdateYtDlpAsync()
        {
            try
            {
                if (!File.Exists(_ytdlpPath))
                {
                    return "yt-dlp not found - cannot update";
                }

                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Starting yt-dlp update...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytdlpPath,
                    Arguments = "-U", // Update command
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_ytdlpPath)
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var result = output.Trim();
                if (!string.IsNullOrEmpty(error))
                {
                    result += $"\nError: {error.Trim()}";
                }

                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] yt-dlp update completed. Exit code: {process.ExitCode}");
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Update output: {result}");

                if (process.ExitCode == 0)
                {
                    return $"Update successful: {result}";
                }
                else
                {
                    return $"Update failed (exit code {process.ExitCode}): {result}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Error updating yt-dlp: {ex.Message}");
                return $"Update error: {ex.Message}";
            }
        }

        /// <summary>
        /// Checks if yt-dlp update is available
        /// </summary>
        /// <returns>True if update is available</returns>
        public async Task<bool> IsYtDlpUpdateAvailableAsync()
        {
            try
            {
                if (!File.Exists(_ytdlpPath))
                {
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytdlpPath,
                    Arguments = "--check-update", // Check for updates
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_ytdlpPath)
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // If there's output, it means an update is available
                return !string.IsNullOrEmpty(output.Trim());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Error checking yt-dlp update: {ex.Message}");
                return false;
            }
        }
    }
} 