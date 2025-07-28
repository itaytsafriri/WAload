using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Aggregate

namespace WAload.Services
{
    public class SocialMediaVideoService
    {
        private readonly string _ytdlpPath;
        private readonly string _downloadFolder;

        public SocialMediaVideoService(string downloadFolder)
        {
            // Get the directory where the current assembly is located
            var assemblyDirectory = AppContext.BaseDirectory;
            
            // Construct path to yt-dlp executable
            _ytdlpPath = Path.Combine(assemblyDirectory, "ytdl", "yt-dlp.exe");
            _downloadFolder = downloadFolder;
            
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
                @"(?:https?://)?(?:www\.)?(?:tiktok\.com)/[^\s]+" // TikTok
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
                @"(?:https?://)?(?:www\.)?(?:tiktok\.com)/[^\s]+" // TikTok
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
        /// Downloads a social media video using yt-dlp
        /// </summary>
        /// <param name="url">The video URL to download</param>
        /// <param name="senderName">Name of the sender for file naming</param>
        /// <param name="timestamp">Timestamp for file naming</param>
        /// <returns>The path to the downloaded video file, or null if failed</returns>
        public async Task<string?> DownloadSocialMediaVideoAsync(string url, string senderName, DateTime timestamp)
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

                // yt-dlp command to download video
                var arguments = $"-o \"{outputTemplate}\" --no-playlist \"{url}\"";

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
                    return null;
                }

                // Wait a moment for the file to be fully written
                await Task.Delay(1000);

                // Find the downloaded file
                var downloadedFile = FindDownloadedFile(safeSenderName, timestampStr);
                if (downloadedFile != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Successfully downloaded: {downloadedFile}");
                    return downloadedFile;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Could not find downloaded file");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialMediaVideo] Exception downloading video: {ex.Message}");
                return null;
            }
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