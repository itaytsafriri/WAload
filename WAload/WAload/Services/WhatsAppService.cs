using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Threading.Tasks;
using WAload.Models;

namespace WAload.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private Process? _nodeProcess;
        private bool _isConnected;
        private bool _isMonitoring;
        private string _nodeScriptPath = string.Empty;
        private MessageQueue? _messageQueue;

        public event EventHandler<string>? QrCodeReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;
        public event EventHandler<string>? UserNameReceived;
        public event EventHandler<List<WhatsGroup>>? GroupsUpdated;
        public event EventHandler<MediaMessage>? MediaMessageReceived;
        public event EventHandler<TextMessage>? TextMessageReceived;
        public event EventHandler<bool>? MonitoringStatusChanged;

        public bool IsConnected => _isConnected;
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// Sets the message queue for handling incoming media messages
        /// </summary>
        public void SetMessageQueue(MessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
            System.Diagnostics.Debug.WriteLine("[WhatsAppService] Message queue configured");
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Clean up any existing session data that might be locked
                CleanupSessionData();
                
                // Kill any existing Node processes running whatsapp.js
                KillExistingNodeProcesses();
                
                _nodeScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node", "whatsapp.js");
                
                System.Diagnostics.Debug.WriteLine($"Looking for Node.js script at: {_nodeScriptPath}");
                
                if (!File.Exists(_nodeScriptPath))
                {
                    throw new FileNotFoundException($"Node.js script not found at: {_nodeScriptPath}");
                }

                System.Diagnostics.Debug.WriteLine($"Node.js script found, starting process...");
                await StartNodeProcessAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeAsync error: {ex.Message}");
                throw new InvalidOperationException($"Failed to initialize WhatsApp service: {ex.Message}", ex);
            }
        }

        private void CleanupSessionData()
        {
            try
            {
                var sessionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node", ".wwebjs_auth");
                const int maxRetries = 5;
                const int delay = 200; // milliseconds
                
                System.Diagnostics.Debug.WriteLine($"[CleanupSessionData] Attempting to clean session directory: {sessionPath}");
                
                if (Directory.Exists(sessionPath))
                {
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            Directory.Delete(sessionPath, true); // Recursively delete all contents
                            System.Diagnostics.Debug.WriteLine($"[CleanupSessionData] Session directory deleted: {sessionPath}");
                            break;
                        }
                        catch (IOException ex) when (i < maxRetries - 1)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CleanupSessionData] IOException on delete attempt {i + 1} for directory {sessionPath}: {ex.Message}");
                            Thread.Sleep(delay);
                        }
                        catch (UnauthorizedAccessException ex) when (i < maxRetries - 1)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CleanupSessionData] UnauthorizedAccessException on delete attempt {i + 1} for directory {sessionPath}: {ex.Message}");
                            Thread.Sleep(delay);
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("[CleanupSessionData] Session data cleaned.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CleanupSessionData] Session directory does not exist, nothing to clean.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CleanupSessionData] Error cleaning session data: {ex.Message}");
            }
        }

        private void KillExistingNodeProcesses()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[KillExistingNodeProcesses] Checking for existing Node processes running whatsapp.js...");
                
                // Get all Node.js processes
                var nodeProcesses = Process.GetProcessesByName("node");
                int killedCount = 0;
                
                foreach (var process in nodeProcesses)
                {
                    try
                    {
                        // Check if this Node process is running whatsapp.js
                        if (process.MainModule?.FileName != null && 
                            process.StartInfo?.Arguments?.Contains("whatsapp.js") == true ||
                            GetProcessCommandLine(process.Id)?.Contains("whatsapp.js") == true)
                        {
                            System.Diagnostics.Debug.WriteLine($"[KillExistingNodeProcesses] Found Node process running whatsapp.js (PID: {process.Id}), killing...");
                            process.Kill(true);
                            killedCount++;
                            System.Diagnostics.Debug.WriteLine($"[KillExistingNodeProcesses] Killed Node process (PID: {process.Id})");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[KillExistingNodeProcesses] Error checking/killing process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                if (killedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[KillExistingNodeProcesses] Killed {killedCount} existing Node process(es) running whatsapp.js");
                    // Give a moment for processes to fully terminate
                    System.Threading.Thread.Sleep(1000);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[KillExistingNodeProcesses] No existing Node processes running whatsapp.js found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KillExistingNodeProcesses] Error during Node process cleanup: {ex.Message}");
            }
        }

        private string? GetProcessCommandLine(int processId)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + processId))
                {
                    using (var objects = searcher.Get())
                    {
                        foreach (System.Management.ManagementObject obj in objects)
                        {
                            return obj["CommandLine"]?.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when querying process command line
            }
            return null;
        }

        private async Task StartNodeProcessAsync()
        {
            // Try to find node.exe in the Node folder first, then fall back to system PATH
            string nodeExePath = "node";
            var localNodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node", "node.exe");
            
            if (File.Exists(localNodePath))
            {
                nodeExePath = localNodePath;
                System.Diagnostics.Debug.WriteLine($"Using local Node.js: {nodeExePath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Local Node.js not found at {localNodePath}, using system PATH");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = nodeExePath,
                Arguments = $"\"{_nodeScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                StandardInputEncoding = System.Text.Encoding.UTF8
            };

            System.Diagnostics.Debug.WriteLine($"Starting Node.js process with: {startInfo.FileName} {startInfo.Arguments}");

            _nodeProcess = new Process { StartInfo = startInfo };
            _nodeProcess.OutputDataReceived += OnNodeOutputReceived;
            _nodeProcess.ErrorDataReceived += OnNodeErrorReceived;
            _nodeProcess.Exited += OnNodeProcessExited;

            _nodeProcess.Start();
            _nodeProcess.BeginOutputReadLine();
            _nodeProcess.BeginErrorReadLine();

            System.Diagnostics.Debug.WriteLine($"Node.js process started with ID: {_nodeProcess.Id}");

            // Wait a bit for the process to start
            await Task.Delay(1000);
        }

        private void OnNodeOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            System.Diagnostics.Debug.WriteLine($"Node.js output: {e.Data}");

            // Skip lines that don't start with '{' (likely debug output)
            if (!e.Data.TrimStart().StartsWith("{"))
            {
                System.Diagnostics.Debug.WriteLine($"Node.js debug: {e.Data}");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to parse JSON: {e.Data}");
                
                // Add more detailed JSON debugging for media messages
                if (e.Data.Contains("\"type\":\"media\""))
                {
                    System.Diagnostics.Debug.WriteLine($"MEDIA MESSAGE DETECTED - Raw JSON: {e.Data}");
                }
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var message = JsonSerializer.Deserialize<NodeMessage>(e.Data, options);
                if (message == null) 
                {
                    System.Diagnostics.Debug.WriteLine("Deserialized message is null");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Parsed message type: '{message.Type}' (length: {message.Type?.Length ?? 0})");

                switch (message.Type)
                {
                    case "qr":
                        System.Diagnostics.Debug.WriteLine($"QR code received: {message.Qr?.Substring(0, Math.Min(50, message.Qr?.Length ?? 0))}...");
                        QrCodeReceived?.Invoke(this, message.Qr ?? string.Empty);
                        break;
                    case "status":
                        _isConnected = message.Connected ?? false;
                        System.Diagnostics.Debug.WriteLine($"Status changed: {_isConnected}");
                        ConnectionStatusChanged?.Invoke(this, _isConnected);
                        break;
                    case "userName":
                        System.Diagnostics.Debug.WriteLine($"User name received: {message.Name}");
                        UserNameReceived?.Invoke(this, message.Name ?? string.Empty);
                        break;
                    case "groups":
                        if (message.Groups != null)
                        {
                            var groups = new List<WhatsGroup>();
                            foreach (var group in message.Groups)
                            {
                                groups.Add(new WhatsGroup { Id = group.Id ?? string.Empty, Name = group.Name ?? string.Empty });
                            }
                            System.Diagnostics.Debug.WriteLine($"Groups received: {groups.Count}");
                            GroupsUpdated?.Invoke(this, groups);
                        }
                        break;
                    case "media":
                        System.Diagnostics.Debug.WriteLine($"Media case triggered - Media object is {(message.Media != null ? "not null" : "null")}");
                        if (message.Media != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Raw Media object - Id: '{message.Media.Id}', From: '{message.Media.From}', Author: '{message.Media.Author}', Type: '{message.Media.Type}', Data length: {message.Media.Data?.Length ?? 0}");
                            
                            // Enhanced media message processing with metadata extraction
                            var enhancedMedia = ProcessMediaMessage(message.Media);
                            
                            // Use message queue if available, otherwise fall back to direct event
                            if (_messageQueue != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[WhatsAppService] Queuing media message: {enhancedMedia.Id}");
                                bool queued = _messageQueue.EnqueueMessage(enhancedMedia);
                                if (!queued)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[WhatsAppService] Failed to queue message (likely duplicate): {enhancedMedia.Id}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[WhatsAppService] No message queue configured, using direct event");
                                MediaMessageReceived?.Invoke(this, enhancedMedia);
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Media object is null - cannot process media message");
                        }
                        break;
                    case "text":
                        System.Diagnostics.Debug.WriteLine($"Text message case triggered - Text object is {(message.Text != null ? "not null" : "null")}");
                        if (message.Text != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Raw Text object - Id: '{message.Text.Id}', From: '{message.Text.From}', Text: '{message.Text.Text?.Substring(0, Math.Min(100, message.Text.Text?.Length ?? 0))}...'");
                            
                            var textMessage = new TextMessage
                            {
                                Id = message.Text.Id ?? string.Empty,
                                From = message.Text.From ?? string.Empty,
                                Author = message.Text.Author ?? string.Empty,
                                Type = message.Text.Type ?? string.Empty,
                                Timestamp = message.Text.Timestamp ?? 0,
                                Text = message.Text.Text ?? string.Empty,
                                SenderName = message.Text.SenderName ?? string.Empty
                            };
                            System.Diagnostics.Debug.WriteLine($"Text message received: {textMessage.Text.Substring(0, Math.Min(100, textMessage.Text.Length))}... (From: {textMessage.SenderName})");
                            TextMessageReceived?.Invoke(this, textMessage);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Text object is null - cannot process text message");
                        }
                        break;
                    case "monitoringStatus":
                        _isMonitoring = message.Monitoring ?? false;
                        System.Diagnostics.Debug.WriteLine($"Monitoring status: {_isMonitoring}");
                        MonitoringStatusChanged?.Invoke(this, _isMonitoring);
                        break;
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parsing error: {ex.Message} for data: {e.Data}");
            }
        }

        private void OnNodeErrorReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Log error output
                System.Diagnostics.Debug.WriteLine($"Node.js Error: {e.Data}");
            }
        }

        private void OnNodeProcessExited(object? sender, EventArgs e)
        {
            _isConnected = false;
            _isMonitoring = false;
            ConnectionStatusChanged?.Invoke(this, false);
            MonitoringStatusChanged?.Invoke(this, false);
        }

        public async Task GetGroupsAsync()
        {
            System.Diagnostics.Debug.WriteLine("GetGroupsAsync called");
            if (_nodeProcess?.HasExited == false)
            {
                var command = new { type = "get_groups" };
                var json = JsonSerializer.Serialize(command);
                System.Diagnostics.Debug.WriteLine($"Sending command to Node.js: {json}");
                
                try
                {
                    await _nodeProcess.StandardInput.WriteLineAsync(json);
                    await _nodeProcess.StandardInput.FlushAsync();
                    System.Diagnostics.Debug.WriteLine("Command sent to Node.js and flushed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending command: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Node.js process is not running or has exited");
            }
        }

        public async Task StartMonitoringAsync(string groupId)
        {
            if (_nodeProcess?.HasExited == false)
            {
                var command = new { type = "monitor_group", groupId };
                var json = JsonSerializer.Serialize(command);
                await _nodeProcess.StandardInput.WriteLineAsync(json);
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (_nodeProcess?.HasExited == false)
            {
                var command = new { type = "stop_monitoring" };
                var json = JsonSerializer.Serialize(command);
                await _nodeProcess.StandardInput.WriteLineAsync(json);
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting WhatsApp logout...");
                
                // Stop monitoring if active
                if (_isMonitoring)
                {
                    await StopMonitoringAsync();
                }

                // Send logout command to Node process
                await SendCommandAsync("logout");
                
                // Wait a bit for the command to be processed
                await Task.Delay(1000);
                
                // Kill the Node process
                if (_nodeProcess != null)
                {
                    if (!_nodeProcess.HasExited)
                    {
                        _nodeProcess.Kill(true);
                        System.Diagnostics.Debug.WriteLine("Node process killed");
                    }
                    _nodeProcess.Dispose();
                    _nodeProcess = null;
                }
                
                // Clean up session data
                CleanupSessionData();
                
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                
                System.Diagnostics.Debug.WriteLine("WhatsApp logout completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during logout: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processes and enhances a media message with metadata extraction
        /// </summary>
        private MediaMessage ProcessMediaMessage(dynamic originalMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WhatsAppService] Processing media message: {originalMessage.Id}");
                
                // Create enhanced media message with extracted metadata
                var enhancedMessage = new MediaMessage
                {
                    Id = originalMessage.Id ?? string.Empty,
                    From = originalMessage.From ?? string.Empty,
                    Author = originalMessage.Author ?? string.Empty,
                    Type = originalMessage.Type ?? "unknown",
                    Timestamp = originalMessage.Timestamp ?? DateTimeOffset.Now.ToUnixTimeSeconds(),
                    Filename = originalMessage.Filename ?? string.Empty,
                    Data = originalMessage.Data ?? string.Empty,
                    Size = originalMessage.Size ?? 0,
                    SenderName = originalMessage.SenderName ?? string.Empty,
                    
                    // Enhanced metadata
                    HasMedia = !string.IsNullOrEmpty(originalMessage.Data?.ToString()),
                    MediaType = ExtractMediaType(originalMessage.Type?.ToString(), originalMessage.Filename?.ToString()),
                    MimeType = ExtractMimeType(originalMessage.Type?.ToString(), originalMessage.Filename?.ToString()),
                    Body = originalMessage.Body?.ToString() ?? string.Empty, // Muli feature - Include message body
                    FromMe = false // Node.js MediaInfo doesn't have FromMe property
                };

                return enhancedMessage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WhatsAppService] Error processing media message: {ex.Message}");
                
                // Return basic message if processing fails
                return new MediaMessage
                {
                    Id = originalMessage.Id?.ToString() ?? Guid.NewGuid().ToString(),
                    Type = "unknown",
                    HasMedia = false,
                    Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
                };
            }
        }

        private string ExtractMediaType(string? messageType, string? fileName)
        {
            if (!string.IsNullOrEmpty(messageType))
            {
                return messageType.ToLower() switch
                {
                    "image" => "image",
                    "video" => "video", 
                    "audio" => "audio",
                    "document" => "document",
                    "sticker" => "sticker",
                    _ => "unknown"
                };
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                var ext = Path.GetExtension(fileName).ToLower();
                return ext switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "image",
                    ".mp4" or ".avi" or ".mov" or ".webm" => "video",
                    ".mp3" or ".wav" or ".ogg" or ".m4a" => "audio", 
                    ".pdf" or ".doc" or ".docx" or ".txt" => "document",
                    _ => "unknown"
                };
            }

            return "unknown";
        }

        private string ExtractMimeType(string? messageType, string? fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                var ext = Path.GetExtension(fileName).ToLower();
                return ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".mp4" => "video/mp4",
                    ".webm" => "video/webm",
                    ".mp3" => "audio/mpeg",
                    ".wav" => "audio/wav",
                    ".ogg" => "audio/ogg",
                    ".pdf" => "application/pdf",
                    ".txt" => "text/plain",
                    _ => "application/octet-stream"
                };
            }

            return messageType?.ToLower() switch
            {
                "image" => "image/jpeg",
                "video" => "video/mp4", 
                "audio" => "audio/mpeg",
                "document" => "application/pdf",
                _ => "application/octet-stream"
            };
        }

        public async Task DisposeAsync()
        {
            if (_nodeProcess != null && !_nodeProcess.HasExited)
            {
                try
                {
                    await LogoutAsync();
                    await Task.Delay(2000); // Give time for logout to complete
                    _nodeProcess.Kill();
                }
                catch
                {
                    // Ignore errors during disposal
                }
                finally
                {
                    _nodeProcess.Dispose();
                    _nodeProcess = null;
                }
            }
        }

        private async Task SendCommandAsync(string commandType, object? data = null)
        {
            if (_nodeProcess?.HasExited == false)
            {
                var command = new { type = commandType, data };
                var json = JsonSerializer.Serialize(command);
                await _nodeProcess.StandardInput.WriteLineAsync(json);
            }
        }
    }

    // Helper classes for JSON deserialization
} 