using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace WAload.Services
{
    public class VideoProcessingService
    {
        private readonly string _ffmpegPath;
        private Process? _currentProcess;

        public VideoProcessingService()
        {
            // Get the directory where the current assembly is located
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            
            // Construct path to ffmpeg executable
            _ffmpegPath = Path.Combine(assemblyDirectory ?? "", "ffmpeg", "ffmpeg.exe");
            
            if (!File.Exists(_ffmpegPath))
            {
                System.Diagnostics.Debug.WriteLine($"Warning: FFmpeg not found at {_ffmpegPath}");
            }
        }

        /// <summary>
        /// Converts a video or image to 16:9 aspect ratio with blurred background
        /// </summary>
        /// <param name="inputPath">Path to the input video or image file</param>
        /// <param name="outputPath">Path where the processed file will be saved</param>
        /// <param name="progress">Optional callback for progress reporting (0.0 - 1.0)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if conversion was successful, false otherwise</returns>
        public async Task<bool> ConvertTo16x9WithBlurredBackground(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(inputPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Input file not found: {inputPath}");
                    progress?.Invoke(1.0); // Ensure progress callback is called even on failure
                    return false;
                }

                if (!File.Exists(_ffmpegPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] FFmpeg is not available. Skipping conversion.");
                    progress?.Invoke(1.0); // Ensure progress callback is called even on failure
                    return false;
                }

                // Check if input file is accessible
                try
                {
                    using var testStream = File.OpenRead(inputPath);
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Input file is accessible, size: {testStream.Length} bytes");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Input file is not accessible: {ex.Message}");
                    progress?.Invoke(1.0);
                    return false;
                }

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Determine if this is an image or video based on file extension
                var fileExtension = Path.GetExtension(inputPath).ToLowerInvariant();
                var isImage = fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".bmp" || fileExtension == ".gif";
                
                if (isImage)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Processing image file: {Path.GetFileName(inputPath)}");
                    var result = await ConvertImageTo16x9WithBlurredBackground(inputPath, outputPath, progress, cancellationToken);
                    progress?.Invoke(1.0); // Ensure progress callback is called on completion
                    return result;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Processing video file: {Path.GetFileName(inputPath)}");
                    var result = await ConvertVideoTo16x9WithBlurredBackground(inputPath, outputPath, progress, cancellationToken);
                    progress?.Invoke(1.0); // Ensure progress callback is called on completion
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Conversion was cancelled for: {Path.GetFileName(inputPath)}");
                progress?.Invoke(1.0);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error during conversion: {ex.Message}");
                progress?.Invoke(1.0); // Ensure progress callback is called even on exception
                return false;
            }
            finally
            {
                // Always clean up the current process reference
                _currentProcess = null;
            }
        }

        /// <summary>
        /// Cancels the current processing operation
        /// </summary>
        public void CancelCurrentProcessing()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    System.Diagnostics.Debug.WriteLine("[VideoProcessing] Cancelling current FFmpeg process");
                    _currentProcess.Kill();
                    _currentProcess.WaitForExit(5000); // Wait up to 5 seconds for clean exit
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error cancelling process: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a video to 16:9 aspect ratio with blurred background
        /// </summary>
        private async Task<bool> ConvertVideoTo16x9WithBlurredBackground(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Process? blurProcess = null;
            try
            {
                _currentProcess = null;
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Starting blur background conversion: {Path.GetFileName(inputPath)}");
                
                progress?.Invoke(0.1); // 10% - Starting

                // Use system temp directory for all processing
                var tempDir = Path.GetTempPath();
                var tempOutputPath = Path.Combine(tempDir, $"temp_{Guid.NewGuid()}.mp4");
                
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Using temp directory: {tempDir}");
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Temp output path: {tempOutputPath}");

                try
                {
                    // Single FFmpeg command with blur background (advanced, with overlay)
                    var blurArgs = $"-y -i \"{inputPath}\" -filter_complex \"[0:v]scale=1920:1080:force_original_aspect_ratio=increase,gblur=sigma=20,crop=1920:1080[bg];[0:v]scale=1920:1080:force_original_aspect_ratio=decrease[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2:format=auto,format=yuv420p\" -c:v libx264 -preset medium -crf 23 -movflags +faststart \"{tempOutputPath}\"";
                    
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Running blur command: {blurArgs}");
                    
                    progress?.Invoke(0.2); // 20% - FFmpeg starting
                    
                    blurProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = _ffmpegPath,
                            Arguments = blurArgs,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            WorkingDirectory = Path.GetDirectoryName(_ffmpegPath)
                        }
                    };

                    _currentProcess = blurProcess;
                    
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Starting FFmpeg process...");
                    var started = blurProcess.Start();
                    
                    if (!started)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Failed to start FFmpeg process");
                        return false;
                    }

                    // Read output and error streams asynchronously
                    var stdOutTask = blurProcess.StandardOutput.ReadToEndAsync();
                    var stdErrTask = blurProcess.StandardError.ReadToEndAsync();

                    // Wait for FFmpeg to finish
                    blurProcess.WaitForExit();
                    await stdOutTask;
                    var stdErr = await stdErrTask;

                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] FFmpeg exited with code: {blurProcess.ExitCode}");
                    if (blurProcess.ExitCode != 0)
                        {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] FFmpeg error output: {stdErr}");
                        return false;
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Processing was cancelled");
                        return false;
                    }

                    progress?.Invoke(0.8); // 80% - FFmpeg completed
                    
                        // Log success output
                        var standardOutput = await blurProcess.StandardOutput.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(standardOutput))
                        {
                            System.Diagnostics.Debug.WriteLine($"[VideoProcessing] FFmpeg standard output: {standardOutput}");
                    }

                    progress?.Invoke(0.9); // 90% - Validating file
                    
                    // Verify the temporary file is valid
                    if (File.Exists(tempOutputPath))
                    {
                        var fileInfo = new FileInfo(tempOutputPath);
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Temp file size: {fileInfo.Length} bytes");
                        
                        if (fileInfo.Length > 1000) // Ensure file is not empty/corrupted
                        {
                            System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Temp file is valid, moving to final destination");
                            
                            // Move the completed file to the final destination
                            File.Move(tempOutputPath, outputPath);
                            
                            System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Successfully moved file to: {outputPath}");
                            progress?.Invoke(1.0); // 100% - Complete
                            return true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Temp file is too small ({fileInfo.Length} bytes), conversion failed");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Temp file does not exist: {tempOutputPath}");
                    }

                    return false;
                }
                finally
                {
                    // Clean up temporary file if it still exists
                    if (File.Exists(tempOutputPath))
                    {
                        try
                        {
                            File.Delete(tempOutputPath);
                            System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Cleaned up temp file: {tempOutputPath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error cleaning up temp file: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Processing was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error in video conversion: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                // Clean up process
                if (blurProcess != null && !blurProcess.HasExited)
                {
                    try
                    {
                        blurProcess.Kill();
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Killed FFmpeg process");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error killing process: {ex.Message}");
                    }
                }
                _currentProcess = null;
            }
        }

        /// <summary>
        /// Converts an image to 16:9 aspect ratio with blurred background
        /// </summary>
        private async Task<bool> ConvertImageTo16x9WithBlurredBackground(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _currentProcess = null;
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Starting image blur background conversion: {Path.GetFileName(inputPath)}");

                // Use system temp directory for all processing
                var tempDir = Path.GetTempPath();
                var tempOutputPath = Path.Combine(tempDir, $"temp_{Guid.NewGuid()}.mp4");
                
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Using temp directory: {tempDir}");
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Temp output path: {tempOutputPath}");

                // Convert image to 16:9 aspect ratio with blurred background effect
                var filterComplex = "[0:v]scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,boxblur=10[bg];[0:v]scale=-1:1080,setsar=1[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2";
                var imageArgs = $"-i \"{inputPath}\" -filter_complex \"{filterComplex}\" -frames:v 1 -update 1 \"{outputPath}\"";
                
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Running image conversion command: {imageArgs}");
                
                var imageProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = imageArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_ffmpegPath)
                    }
                };

                _currentProcess = imageProcess;
                imageProcess.Start();

                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Waiting for image conversion process to exit...");
                
                while (!imageProcess.HasExited && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(200, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image processing was cancelled");
                    return false;
                }

                await imageProcess.WaitForExitAsync(cancellationToken);
                
                if (cancellationToken.IsCancellationRequested) return false;

                var exitCode = imageProcess.ExitCode;
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image conversion process exited with code: {exitCode}");

                if (exitCode != 0)
                {
                    var errorOutput = await imageProcess.StandardError.ReadToEndAsync();
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image conversion failed: {errorOutput}");
                    return false;
                }

                // Verify the output file is valid
                if (File.Exists(outputPath))
                {
                    var fileInfo = new FileInfo(outputPath);
                    if (fileInfo.Length > 1000) // Ensure file is not empty/corrupted
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image conversion completed successfully: {Path.GetFileName(outputPath)} ({fileInfo.Length} bytes)");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image output file is too small ({fileInfo.Length} bytes), conversion failed");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image output file does not exist: {outputPath}");
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Image processing was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error during image conversion: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if FFmpeg is available
        /// </summary>
        public bool IsFFmpegAvailable()
        {
            return File.Exists(_ffmpegPath);
        }

        /// <summary>
        /// Gets the FFmpeg version
        /// </summary>
        public async Task<string> GetFFmpegVersion()
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

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                await process.WaitForExitAsync();

                var lines = output.Split('\n');
                return lines.Length > 0 ? lines[0] : "Unknown version";
            }
            catch (Exception ex)
            {
                return $"Error getting version: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the duration of a video file
        /// </summary>
        public async Task<TimeSpan> GetVideoDurationAsync(string videoPath)
        {
            try
            {
                if (!File.Exists(videoPath) || !File.Exists(_ffmpegPath))
                    return TimeSpan.Zero;

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-i \"{videoPath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Parse duration from FFmpeg output
                var durationMatch = System.Text.RegularExpressions.Regex.Match(error, @"Duration: (\d{2}):(\d{2}):(\d{2})\.(\d{2})");
                if (durationMatch.Success)
                {
                    var hours = int.Parse(durationMatch.Groups[1].Value);
                    var minutes = int.Parse(durationMatch.Groups[2].Value);
                    var seconds = int.Parse(durationMatch.Groups[3].Value);
                    var centiseconds = int.Parse(durationMatch.Groups[4].Value);
                    return new TimeSpan(0, hours, minutes, seconds, centiseconds * 10);
                }

                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error getting video duration: {ex.Message}");
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Generates a thumbnail from a video file
        /// </summary>
        public async Task<string?> GenerateThumbnailAsync(string videoPath, string thumbnailPath, TimeSpan position)
        {
            try
            {
                if (!File.Exists(videoPath) || !File.Exists(_ffmpegPath))
                    return null;

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(thumbnailPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Format time for FFmpeg
                var timeString = $"{position.Hours:D2}:{position.Minutes:D2}:{position.Seconds:D2}.{position.Milliseconds:D3}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-y -i \"{videoPath}\" -vframes 1 -ss {timeString} -vf \"scale=320:-1:flags=lanczos\" -q:v 2 \"{thumbnailPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_ffmpegPath)
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoProcessing] FFmpeg thumbnail generation failed: {error}");
                    
                    // Try fallback: extract first frame without seeking
                    var fallbackStartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = $"-y -i \"{videoPath}\" -vframes 1 -vf \"scale=320:-1:flags=lanczos\" -q:v 2 \"{thumbnailPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_ffmpegPath)
                    };

                    using var fallbackProcess = new Process { StartInfo = fallbackStartInfo };
                    fallbackProcess.Start();
                    var fallbackError = await fallbackProcess.StandardError.ReadToEndAsync();
                    await fallbackProcess.WaitForExitAsync();

                    if (fallbackProcess.ExitCode != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoProcessing] FFmpeg fallback thumbnail generation also failed: {fallbackError}");
                        return null;
                    }
                }

                return File.Exists(thumbnailPath) ? thumbnailPath : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoProcessing] Error generating thumbnail: {ex.Message}");
                return null;
            }
        }
    }
} 