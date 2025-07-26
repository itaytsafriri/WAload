using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public event EventHandler<string>? QrCodeReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;
        public event EventHandler<string>? UserNameReceived;
        public event EventHandler<List<WhatsGroup>>? GroupsUpdated;
        public event EventHandler<MediaMessage>? MediaMessageReceived;
        public event EventHandler<bool>? MonitoringStatusChanged;

        public bool IsConnected => _isConnected;
        public bool IsMonitoring => _isMonitoring;

        public async Task InitializeAsync()
        {
            try
            {
                // Clean up any existing session data that might be locked
                CleanupSessionData();
                
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

        private async Task StartNodeProcessAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{_nodeScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
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
                    ReadCommentHandling = JsonCommentHandling.Skip
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
                            
                            var mediaMessage = new MediaMessage
                            {
                                Id = message.Media.Id ?? string.Empty,
                                From = message.Media.From ?? string.Empty,
                                Author = message.Media.Author ?? string.Empty,
                                Type = message.Media.Type ?? string.Empty,
                                Timestamp = message.Media.Timestamp ?? 0,
                                Filename = message.Media.Filename ?? string.Empty,
                                Data = message.Media.Data ?? string.Empty,
                                Size = message.Media.Size ?? 0,
                                SenderName = message.Media.SenderName ?? string.Empty
                            };
                            System.Diagnostics.Debug.WriteLine($"Media received: {mediaMessage.Filename} (Type: {mediaMessage.Type}, Size: {mediaMessage.Size}, Data length: {mediaMessage.Data?.Length ?? 0})");
                            System.Diagnostics.Debug.WriteLine($"Media details - ID: {mediaMessage.Id}, From: {mediaMessage.From}, Author: {mediaMessage.Author}");
                            if (string.IsNullOrEmpty(mediaMessage.Data))
                            {
                                System.Diagnostics.Debug.WriteLine("WARNING: Media data is empty - this might indicate a download issue");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"SUCCESS: Media data received with length {mediaMessage.Data.Length}");
                            }
                            MediaMessageReceived?.Invoke(this, mediaMessage);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Media object is null - cannot process media message");
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