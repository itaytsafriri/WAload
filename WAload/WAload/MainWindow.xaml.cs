using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using WAload.Models;
using WAload.Services;
using QRCoder;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace WAload
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly VideoProcessingService _videoProcessingService;
        private readonly ObservableCollection<MediaItem> _mediaItems;
        private readonly ObservableCollection<WhatsGroup> _groups;
        private string _downloadFolder = string.Empty;
        private string _statusMessage = "Ready";
        private bool _isConnected;
        private bool _isMonitoring;
        private bool _isGroupsLoaded;
        private string _loggedInUserName = string.Empty;
        private bool _isLoggingOut = false;

        public ObservableCollection<MediaItem> MediaItems => _mediaItems;
        public ObservableCollection<WhatsGroup> Groups => _groups;

        public string DownloadFolder
        {
            get => _downloadFolder;
            set
            {
                if (_downloadFolder != value)
                {
                    _downloadFolder = value;
                    OnPropertyChanged(nameof(DownloadFolder));
                    LoadExistingMedia();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(ConnectionStatus));
                    OnPropertyChanged(nameof(IsGroupsLoadedAndNotMonitoring));
                }
            }
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                if (_isMonitoring != value)
                {
                    _isMonitoring = value;
                    OnPropertyChanged(nameof(IsMonitoring));
                    OnPropertyChanged(nameof(IsGroupsLoadedAndNotMonitoring));
                }
            }
        }

        public bool IsGroupsLoaded
        {
            get => _isGroupsLoaded;
            set
            {
                if (_isGroupsLoaded != value)
                {
                    _isGroupsLoaded = value;
                    OnPropertyChanged(nameof(IsGroupsLoaded));
                    OnPropertyChanged(nameof(IsGroupsLoadedAndNotMonitoring));
                }
            }
        }

        public bool IsGroupsLoadedAndNotMonitoring => IsGroupsLoaded && !IsMonitoring;

        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            
            _whatsAppService = new WhatsAppService();
            _videoProcessingService = new VideoProcessingService();
            _mediaItems = new ObservableCollection<MediaItem>();
            _groups = new ObservableCollection<WhatsGroup>();

            DataContext = this;
            
            // Set default download folder
            DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WAload Downloads");
            
            // Ensure download folder exists
            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            SetupEventHandlers();
            LoadExistingMedia();
        }

        private void SetupEventHandlers()
        {
            _whatsAppService.QrCodeReceived += OnQrCodeReceived;
            _whatsAppService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _whatsAppService.UserNameReceived += OnUserNameReceived;
            _whatsAppService.GroupsUpdated += OnGroupsUpdated;
            _whatsAppService.MediaMessageReceived += OnMediaMessageReceived;
            _whatsAppService.MonitoringStatusChanged += OnMonitoringStatusChanged;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Connecting to WhatsApp...";
                ConnectButton.IsEnabled = false;
                
                await _whatsAppService.InitializeAsync();
                
                StatusMessage = "Waiting for QR code...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
                ConnectButton.IsEnabled = true;
                System.Windows.MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GetGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Fetching groups...";
                await _whatsAppService.GetGroupsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to get groups: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to get groups: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupsComboBox.SelectedValue is string groupId)
            {
                try
                {
                    StatusMessage = "Starting monitoring...";
                    await _whatsAppService.StartMonitoringAsync(groupId);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to start monitoring: {ex.Message}";
                    System.Windows.MessageBox.Show($"Failed to start monitoring: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a group to monitor.", "No Group Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Stopping monitoring...";
                await _whatsAppService.StopMonitoringAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to stop monitoring: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to stop monitoring: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Logging out...";
                await _whatsAppService.LogoutAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Logout failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Logout failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select download folder";
            dialog.SelectedPath = DownloadFolder;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadFolder = dialog.SelectedPath;
            }
        }

        private void GroupsComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Handle group selection if needed
        }

        private void MediaListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                OpenFile(item);
            }
        }

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                OpenFile(item);
            }
        }

        private void OpenFileLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                OpenFileLocation(item);
            }
        }

        private void CopyFilePathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                CopyFilePath(item);
            }
        }

        private void RenameFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                RenameFile(item);
            }
        }

        private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                DeleteFile(item);
            }
        }

        private void CancelQrButton_Click(object sender, RoutedEventArgs e)
        {
            QrCodeOverlay.Visibility = Visibility.Collapsed;
            ConnectButton.IsEnabled = true;
            StatusMessage = "QR code scan cancelled";
        }

        private void OnQrCodeReceived(object? sender, string qrCode)
        {
            System.Diagnostics.Debug.WriteLine($"QR code event received in MainWindow: {qrCode.Substring(0, Math.Min(50, qrCode.Length))}...");
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Generating QR code image...");
                    var qrGenerator = new QRCodeGenerator();
                    var qrCodeData = qrGenerator.CreateQrCode(qrCode, QRCodeGenerator.ECCLevel.Q);
                    var qrCodeBitmap = new QRCode(qrCodeData).GetGraphic(20);
                    
                    using var memory = new MemoryStream();
                    qrCodeBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memory;
                    bitmapImage.EndInit();
                    
                    QrCodeImage.Source = bitmapImage;
                    QrCodeOverlay.Visibility = Visibility.Visible;
                    StatusMessage = "QR code displayed - scan with your phone";
                    System.Diagnostics.Debug.WriteLine("QR code overlay made visible");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"QR code generation error: {ex.Message}");
                    StatusMessage = $"Failed to generate QR code: {ex.Message}";
                }
            });
        }

        private async void OnConnectionStatusChanged(object? sender, bool connected)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                IsConnected = connected;
                ConnectButton.IsEnabled = !connected;
                
                if (connected)
                {
                    QrCodeOverlay.Visibility = Visibility.Collapsed;
                    StatusMessage = $"Connected as {_loggedInUserName}";
                    
                    // Automatically start fetching groups when connected
                    try
                    {
                        ShowProgressModal("Loading Groups", "Fetching WhatsApp groups...");
                        await _whatsAppService.GetGroupsAsync();
                    }
                    catch (Exception ex)
                    {
                        HideProgressModal();
                        StatusMessage = $"Failed to load groups: {ex.Message}";
                    }
                }
                else
                {
                    StatusMessage = "Disconnected";
                    IsMonitoring = false;
                    IsGroupsLoaded = false;
                    _groups.Clear();
                }
            });
        }

        private void OnUserNameReceived(object? sender, string userName)
        {
            Dispatcher.Invoke(() =>
            {
                _loggedInUserName = userName;
                if (IsConnected)
                {
                    StatusMessage = $"Connected as {userName}";
                }
            });
        }

        private void OnGroupsUpdated(object? sender, List<WhatsGroup> groups)
        {
            Dispatcher.Invoke(() =>
            {
                _groups.Clear();
                foreach (var group in groups)
                {
                    _groups.Add(group);
                }
                
                IsGroupsLoaded = groups.Count > 0;
                StatusMessage = $"Loaded {groups.Count} groups";
                HideProgressModal();
                // Auto-open the dropdown to show user they need to select a group
                if (groups.Count > 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        GroupsComboBox.IsDropDownOpen = true;
                    }));
                }
            });
        }

        private async void OnMediaMessageReceived(object? sender, MediaMessage mediaMessage)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    StatusMessage = "Downloading media...";
                    
                    var mediaItem = await DownloadMediaAsync(mediaMessage);
                    if (mediaItem != null)
                    {
                        _mediaItems.Add(mediaItem);
                        StatusMessage = $"Downloaded: {mediaItem.FileName}";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Download failed: {ex.Message}";
                }
            });
        }

        private void OnMonitoringStatusChanged(object? sender, bool monitoring)
        {
            Dispatcher.Invoke(() =>
            {
                IsMonitoring = monitoring;
                StatusMessage = monitoring ? "Monitoring active" : "Monitoring stopped";
            });
        }

        private async Task<MediaItem?> DownloadMediaAsync(MediaMessage mediaMessage)
        {
            try
            {
                // Decode base64 data
                var mediaData = Convert.FromBase64String(mediaMessage.Data);
                
                // Determine file extension
                var extension = GetFileExtension(mediaMessage.Type);
                var fileName = GetSafeFileName(mediaMessage.Filename, extension);
                var filePath = Path.Combine(DownloadFolder, fileName);
                
                // Ensure unique filename
                var counter = 1;
                while (File.Exists(filePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                    filePath = Path.Combine(DownloadFolder, newFileName);
                    counter++;
                }
                
                // Write file
                await File.WriteAllBytesAsync(filePath, mediaData);
                
                // Create media item
                var mediaItem = new MediaItem
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    MediaType = mediaMessage.Type,
                    FileSize = mediaMessage.Size,
                    Timestamp = mediaMessage.DateTime,
                    SenderName = mediaMessage.SenderName,
                    Extension = extension
                };
                
                // Generate thumbnail
                await GenerateThumbnailAsync(mediaItem);
                
                return mediaItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading media: {ex.Message}");
                return null;
            }
        }

        private string GetFileExtension(string mediaType)
        {
            return mediaType.ToLower() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "video/avi" => ".avi",
                "video/mov" => ".mov",
                "video/wmv" => ".wmv",
                "audio/mp3" => ".mp3",
                "audio/wav" => ".wav",
                "audio/ogg" => ".ogg",
                "application/pdf" => ".pdf",
                "application/msword" => ".doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                _ => ".bin"
            };
        }

        private string GetSafeFileName(string fileName, string extension)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"media_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
            
            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            fileName = invalidChars.Aggregate(fileName, (current, c) => current.Replace(c, '_'));
            
            // Ensure it has the correct extension
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                fileName += extension;
            }
            
            return fileName;
        }

        private async Task GenerateThumbnailAsync(MediaItem mediaItem)
        {
            try
            {
                if (mediaItem.IsImage)
                {
                    // For images, create thumbnail from the image itself
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(mediaItem.FilePath);
                    bitmap.DecodePixelWidth = 120;
                    bitmap.DecodePixelHeight = 80;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    mediaItem.Thumbnail = bitmap;
                }
                else if (mediaItem.IsVideo)
                {
                    // For videos, generate thumbnail using FFmpeg
                    var thumbnailPath = Path.Combine(DownloadFolder, $"thumb_{Path.GetFileNameWithoutExtension(mediaItem.FileName)}.jpg");
                    
                    var duration = await _videoProcessingService.GetVideoDurationAsync(mediaItem.FilePath);
                    var thumbnailPosition = TimeSpan.FromSeconds(duration.TotalSeconds / 2);
                    
                    var result = await _videoProcessingService.GenerateThumbnailAsync(mediaItem.FilePath, thumbnailPath, thumbnailPosition);
                    
                    if (result != null)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(thumbnailPath);
                        bitmap.DecodePixelWidth = 120;
                        bitmap.DecodePixelHeight = 80;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        
                        mediaItem.Thumbnail = bitmap;
                    }
                }
                else
                {
                    // For documents, use a default icon
                    // You can add a default document icon here
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating thumbnail: {ex.Message}");
            }
        }

        private void LoadExistingMedia()
        {
            if (string.IsNullOrEmpty(DownloadFolder) || !Directory.Exists(DownloadFolder))
                return;

            try
            {
                _mediaItems.Clear();
                
                var files = Directory.GetFiles(DownloadFolder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var mediaItem = new MediaItem
                    {
                        FileName = fileInfo.Name,
                        FilePath = file,
                        FileSize = fileInfo.Length,
                        Timestamp = fileInfo.CreationTime,
                        Extension = fileInfo.Extension
                    };
                    
                    // Determine media type from extension
                    mediaItem.MediaType = GetMediaTypeFromExtension(fileInfo.Extension);
                    
                    _mediaItems.Add(mediaItem);
                }
                
                StatusMessage = $"Loaded {_mediaItems.Count} existing files";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading existing media: {ex.Message}";
            }
        }

        private string GetMediaTypeFromExtension(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".avi" => "video/avi",
                ".mov" => "video/mov",
                ".wmv" => "video/wmv",
                ".mp3" => "audio/mp3",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }

        private void OpenFile(MediaItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open file: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileLocation(MediaItem item)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open file location: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyFilePath(MediaItem item)
        {
            try
            {
                System.Windows.Clipboard.SetText(item.FilePath);
                StatusMessage = "File path copied to clipboard";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to copy file path: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameFile(MediaItem item)
        {
            // Implement file renaming functionality
            StatusMessage = "File renaming not implemented yet";
        }

        private void DeleteFile(MediaItem item)
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{item.FileName}'?", 
                "Confirm Delete", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(item.FilePath);
                    _mediaItems.Remove(item);
                    StatusMessage = $"Deleted: {item.FileName}";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to delete file: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowProgressModal(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressModal.Visibility = Visibility.Visible;
                ProgressTitle.Text = title;
                ProgressMessage.Text = message;
            });
        }

        private void HideProgressModal()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressModal.Visibility = Visibility.Collapsed;
            });
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isLoggingOut)
                return;
            e.Cancel = true;
            _isLoggingOut = true;
            ShowProgressModal("Logging out", "Logging out, please wait...");
            try
            {
                var logoutTask = _whatsAppService.LogoutAsync();
                var completedTask = await Task.WhenAny(logoutTask, Task.Delay(10000));
                // If logout completes or timeout, proceed
            }
            catch { /* Ignore errors during shutdown */ }
            HideProgressModal();
            _isLoggingOut = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}