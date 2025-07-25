using System;
using System.IO;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using System.Linq;
using System.Diagnostics;

namespace WAload.Services
{
    public class VideoProcessingService
    {
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;

        public VideoProcessingService()
        {
            // Get the directory where the current assembly is located
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            
            // Construct paths to ffmpeg executables
            _ffmpegPath = Path.Combine(assemblyDirectory ?? "", "ffmpeg", "ffmpeg.exe");
            _ffprobePath = Path.Combine(assemblyDirectory ?? "", "ffmpeg", "ffprobe.exe");
            
            // Set the ffmpeg path for Xabe.FFmpeg library
            if (File.Exists(_ffmpegPath))
            {
                FFmpeg.SetExecutablesPath(Path.GetDirectoryName(_ffmpegPath) ?? "");
                System.Diagnostics.Debug.WriteLine($"FFmpeg path set to: {Path.GetDirectoryName(_ffmpegPath)}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Warning: FFmpeg not found at {_ffmpegPath}");
            }
        }
        public async Task<bool> ConvertTo169AspectRatioAsync(string inputPath, string outputPath, IProgress<double>? progress = null)
        {
            try
            {
                // Get media info
                var mediaInfo = await FFmpeg.GetMediaInfo(inputPath);
                var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
                
                if (videoStream == null)
                {
                    return false;
                }

                // Calculate new dimensions for 16:9 aspect ratio
                var (newWidth, newHeight) = Calculate169Dimensions(videoStream.Width, videoStream.Height);

                // Create conversion
                var conversion = await FFmpeg.Conversions.FromSnippet.ToMp4(inputPath, outputPath);
                
                // Set video codec and dimensions
                conversion.AddParameter($"-vf \"scale={newWidth}:{newHeight}:force_original_aspect_ratio=decrease,pad={newWidth}:{newHeight}:(ow-iw)/2:(oh-ih)/2\"");
                conversion.AddParameter("-c:v libx264");
                conversion.AddParameter("-preset medium");
                conversion.AddParameter("-crf 23");

                // Set audio codec
                if (mediaInfo.AudioStreams != null && mediaInfo.AudioStreams.Count() > 0)
                {
                    conversion.AddParameter("-c:a aac");
                    conversion.AddParameter("-b:a 128k");
                }

                // Execute conversion
                await conversion.Start();

                return File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Video conversion error: {ex.Message}");
                return false;
            }
        }

        private (int width, int height) Calculate169Dimensions(int originalWidth, int originalHeight)
        {
            const double targetAspectRatio = 16.0 / 9.0;
            const int maxWidth = 1920; // Maximum width for 1080p
            const int maxHeight = 1080; // Maximum height for 1080p

            // Calculate dimensions maintaining aspect ratio
            int newWidth, newHeight;

            if (originalWidth / (double)originalHeight > targetAspectRatio)
            {
                // Original is wider than 16:9, fit to height
                newHeight = Math.Min(originalHeight, maxHeight);
                newWidth = (int)(newHeight * targetAspectRatio);
            }
            else
            {
                // Original is taller than 16:9, fit to width
                newWidth = Math.Min(originalWidth, maxWidth);
                newHeight = (int)(newWidth / targetAspectRatio);
            }

            // Ensure dimensions are even numbers (required by some codecs)
            newWidth = newWidth - (newWidth % 2);
            newHeight = newHeight - (newHeight % 2);

            return (newWidth, newHeight);
        }

        public async Task<string?> GenerateThumbnailAsync(string videoPath, string thumbnailPath, TimeSpan position)
        {
            try
            {
                var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(videoPath, thumbnailPath, position);
                conversion.AddParameter("-vframes 1");
                conversion.AddParameter("-q:v 2");

                await conversion.Start();

                return File.Exists(thumbnailPath) ? thumbnailPath : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail generation error: {ex.Message}");
                return null;
            }
        }

        public async Task<TimeSpan> GetVideoDurationAsync(string videoPath)
        {
            try
            {
                var mediaInfo = await FFmpeg.GetMediaInfo(videoPath);
                return mediaInfo.Duration;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting video duration: {ex.Message}");
                return TimeSpan.Zero;
            }
        }

        public bool IsFFmpegAvailable()
        {
            return File.Exists(_ffmpegPath) && File.Exists(_ffprobePath);
        }

        public string GetFFmpegVersion()
        {
            try
            {
                if (!File.Exists(_ffmpegPath))
                    return "FFmpeg not found";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    // Extract version from first line
                    var lines = output.Split('\n');
                    if (lines.Length > 0)
                    {
                        return lines[0].Trim();
                    }
                }
                
                return "Unknown version";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting FFmpeg version: {ex.Message}");
                return "Error getting version";
            }
        }
    }
} 