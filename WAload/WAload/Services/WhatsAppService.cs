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
                _nodeScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node", "whatsapp.js");
                
                if (!File.Exists(_nodeScriptPath))
                {
                    throw new FileNotFoundException($"Node.js script not found at: {_nodeScriptPath}");
                }

                await StartNodeProcessAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize WhatsApp service: {ex.Message}", ex);
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

            _nodeProcess = new Process { StartInfo = startInfo };
            _nodeProcess.OutputDataReceived += OnNodeOutputReceived;
            _nodeProcess.ErrorDataReceived += OnNodeErrorReceived;
            _nodeProcess.Exited += OnNodeProcessExited;

            _nodeProcess.Start();
            _nodeProcess.BeginOutputReadLine();
            _nodeProcess.BeginErrorReadLine();

            // Wait a bit for the process to start
            await Task.Delay(1000);
        }

        private void OnNodeOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            // Skip lines that don't start with '{' (likely debug output)
            if (!e.Data.TrimStart().StartsWith("{"))
            {
                System.Diagnostics.Debug.WriteLine($"Node.js debug: {e.Data}");
                return;
            }

            try
            {
                var message = JsonSerializer.Deserialize<NodeMessage>(e.Data);
                if (message == null) return;

                switch (message.Type)
                {
                    case "qr":
                        QrCodeReceived?.Invoke(this, message.Qr ?? string.Empty);
                        break;
                    case "status":
                        _isConnected = message.Connected ?? false;
                        ConnectionStatusChanged?.Invoke(this, _isConnected);
                        break;
                    case "userName":
                        UserNameReceived?.Invoke(this, message.Name ?? string.Empty);
                        break;
                    case "groups":
                        if (message.Groups != null)
                        {
                            var groups = new List<WhatsGroup>();
                            foreach (var group in message.Groups)
                            {
                                groups.Add(new WhatsGroup { Id = group.Id, Name = group.Name });
                            }
                            GroupsUpdated?.Invoke(this, groups);
                        }
                        break;
                    case "media":
                        if (message.Media != null)
                        {
                            var mediaMessage = new MediaMessage
                            {
                                Id = message.Media.Id,
                                From = message.Media.From,
                                Author = message.Media.Author,
                                Type = message.Media.Type,
                                Timestamp = message.Media.Timestamp,
                                Filename = message.Media.Filename,
                                Data = message.Media.Data,
                                Size = message.Media.Size,
                                SenderName = message.Media.SenderName
                            };
                            MediaMessageReceived?.Invoke(this, mediaMessage);
                        }
                        break;
                    case "monitoringStatus":
                        _isMonitoring = message.Monitoring ?? false;
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
            if (_nodeProcess?.HasExited == false)
            {
                var command = new { type = "get_groups" };
                var json = JsonSerializer.Serialize(command);
                await _nodeProcess.StandardInput.WriteLineAsync(json);
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
            if (_nodeProcess?.HasExited == false)
            {
                var command = new { type = "logout" };
                var json = JsonSerializer.Serialize(command);
                await _nodeProcess.StandardInput.WriteLineAsync(json);
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
    }

    // Helper classes for JSON deserialization
    public class NodeMessage
    {
        public string Type { get; set; } = string.Empty;
        public string? Qr { get; set; }
        public bool? Connected { get; set; }
        public string? Name { get; set; }
        public List<GroupInfo>? Groups { get; set; }
        public MediaInfo? Media { get; set; }
        public bool? Monitoring { get; set; }
    }

    public class GroupInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class MediaInfo
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
    }
} 