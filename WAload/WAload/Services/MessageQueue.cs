using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WAload.Models;

namespace WAload.Services
{
    /// <summary>
    /// MessageQueue service for managing and processing WhatsApp media messages
    /// Implements queuing, duplicate prevention, batch processing, and retry logic
    /// </summary>
    public class MessageQueue : INotifyPropertyChanged
    {
        private readonly ObservableCollection<MediaMessage> _mediaItems = new ObservableCollection<MediaMessage>();
        private readonly HashSet<string> _processedIds = new HashSet<string>();
        private readonly object _lockObject = new object();
        private bool _isProcessingEnabled = true;
        private bool _isProcessing = false;
        private int _queuedCount = 0;
        private int _processedCount = 0;
        private int _failedCount = 0;
        private System.Threading.Timer? _batchProcessingTimer;
        private readonly List<MediaMessage> _pendingBatch = new List<MediaMessage>();
        private readonly SupabaseLoggingService _loggingService;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<MediaMessage>? MessageProcessed;
        public event EventHandler<QueueStatusEventArgs>? QueueStatusChanged;
        public event EventHandler<EventArgs>? ProcessingComplete;

        // Observable collection for UI binding
        public ObservableCollection<MediaMessage> MediaItems => _mediaItems;

        // Properties for UI binding
        public bool IsProcessingEnabled 
        { 
            get => _isProcessingEnabled; 
            set 
            { 
                if (_isProcessingEnabled != value)
                {
                    _isProcessingEnabled = value;
                    OnPropertyChanged(nameof(IsProcessingEnabled));
                    OnQueueStatusChanged();
                }
            } 
        }

        public bool IsProcessing 
        { 
            get => _isProcessing; 
            private set 
            { 
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                    OnQueueStatusChanged();
                }
            } 
        }

        public int QueuedCount 
        { 
            get => _queuedCount; 
            private set 
            { 
                if (_queuedCount != value)
                {
                    _queuedCount = value;
                    OnPropertyChanged(nameof(QueuedCount));
                    OnQueueStatusChanged();
                }
            } 
        }

        public int ProcessedCount 
        { 
            get => _processedCount; 
            private set 
            { 
                if (_processedCount != value)
                {
                    _processedCount = value;
                    OnPropertyChanged(nameof(ProcessedCount));
                    OnQueueStatusChanged();
                }
            } 
        }

        public int FailedCount 
        { 
            get => _failedCount; 
            private set 
            { 
                if (_failedCount != value)
                {
                    _failedCount = value;
                    OnPropertyChanged(nameof(FailedCount));
                    OnQueueStatusChanged();
                }
            } 
        }

        public MessageQueue()
        {
            // Initialize batch processing timer (process every 2 seconds)
            _batchProcessingTimer = new System.Threading.Timer(ProcessBatch, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            
            // Initialize logging service
            _loggingService = new SupabaseLoggingService();
            
            System.Diagnostics.Debug.WriteLine("[MessageQueue] Initialized with batch processing and Supabase logging");
        }

        /// <summary>
        /// Adds a media message to the queue with duplicate prevention
        /// </summary>
        public bool EnqueueMessage(MediaMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Id))
            {
                System.Diagnostics.Debug.WriteLine("[MessageQueue] Rejected null or invalid message");
                return false;
            }

            lock (_lockObject)
            {
                // Check for duplicates
                if (_processedIds.Contains(message.Id))
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageQueue] Duplicate message rejected: {message.Id}");
                    return false;
                }

                // Add to processed IDs set
                _processedIds.Add(message.Id);

                // Enhance message with queue metadata
                var enhancedMessage = new MediaMessage
                {
                    Id = message.Id,
                    Author = message.Author,
                    FromMe = message.FromMe,
                    Type = message.Type,
                    Body = message.Body,
                    Timestamp = message.Timestamp != 0 ? message.Timestamp : DateTimeOffset.Now.ToUnixTimeSeconds(),
                    HasMedia = message.HasMedia,
                    MediaType = message.MediaType,
                    Filename = message.Filename,
                    Size = message.Size,
                    MimeType = message.MimeType,
                    Data = message.Data,
                    SenderName = message.SenderName
                };

                // Add to pending batch for processing
                _pendingBatch.Add(enhancedMessage);

                // Update UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _mediaItems.Add(enhancedMessage);
                });

                QueuedCount++;
                System.Diagnostics.Debug.WriteLine($"[MessageQueue] Message queued: {message.Id} (Total: {QueuedCount})");
                return true;
            }
        }

        /// <summary>
        /// Processes queued messages in batches
        /// </summary>
        private void ProcessBatch(object? state)
        {
            if (!IsProcessingEnabled || IsProcessing)
                return;

            List<MediaMessage> batchToProcess;
            lock (_lockObject)
            {
                if (_pendingBatch.Count == 0)
                    return;

                // Get up to 10 messages for batch processing
                batchToProcess = _pendingBatch.Take(10).ToList();
                _pendingBatch.RemoveRange(0, batchToProcess.Count);
            }

            if (batchToProcess.Count == 0)
                return;

            IsProcessing = true;
            System.Diagnostics.Debug.WriteLine($"[MessageQueue] Processing batch of {batchToProcess.Count} messages");

            Task.Run(async () =>
            {
                try
                {
                    foreach (var message in batchToProcess)
                    {
                        if (!IsProcessingEnabled)
                            break;

                        await ProcessSingleMessage(message);
                        
                        // Small delay between messages to prevent overwhelming
                        await Task.Delay(100);
                    }

                    // Check if all queues are empty
                    bool processingComplete;
                    lock (_lockObject)
                    {
                        processingComplete = _pendingBatch.Count == 0;
                    }

                    if (processingComplete)
                    {
                        System.Diagnostics.Debug.WriteLine("[MessageQueue] All messages processed");
                        ProcessingComplete?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageQueue] Batch processing error: {ex.Message}");
                }
                finally
                {
                    IsProcessing = false;
                }
            });
        }

        /// <summary>
        /// Processes a single message with retry logic
        /// </summary>
        private async Task ProcessSingleMessage(MediaMessage message)
        {
            const int maxRetries = 3;
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageQueue] Processing message {message.Id} (attempt {attempt})");
                    
                    // Simulate processing (download, convert, save, etc.)
                    await SimulateMessageProcessing(message);
                    
                    // Mark as processed
                    ProcessedCount++;
                    MessageProcessed?.Invoke(this, message);
                    
                    System.Diagnostics.Debug.WriteLine($"[MessageQueue] Successfully processed: {message.Id}");
                    
                    // Log successful processing to Supabase
                    await LogMediaProcessing(message, true);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"[MessageQueue] Processing attempt {attempt} failed for {message.Id}: {ex.Message}");
                    
                    if (attempt < maxRetries)
                    {
                        // Exponential backoff
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    }
                }
            }

            // All retries failed
            FailedCount++;
            System.Diagnostics.Debug.WriteLine($"[MessageQueue] Failed to process after {maxRetries} attempts: {message.Id}");
            System.Diagnostics.Debug.WriteLine($"[MessageQueue] Last error: {lastException?.Message}");
            
            // Log failed processing to Supabase
            await LogMediaProcessing(message, false, lastException?.Message);
        }

        private async Task LogMediaProcessing(MediaMessage message, bool successful, string? errors = null)
        {
            try
            {
                var sender = message.SenderName ?? message.Author ?? "unknown";
                var mediaType = message.MediaType ?? "unknown";
                var extension = Path.GetExtension(message.Filename ?? "") ?? "";
                
                // Check if this is a link-based message
                bool? isLink = null;
                string? linkType = null;
                
                if (!string.IsNullOrEmpty(message.Body))
                {
                    // Simple URL detection
                    var urlPattern = @"https?://[^\s]+";
                    var matches = System.Text.RegularExpressions.Regex.Matches(message.Body, urlPattern);
                    if (matches.Count > 0)
                    {
                        isLink = true;
                        var firstUrl = matches[0].Value.ToLower();
                        
                        // Determine link type based on URL
                        if (firstUrl.Contains("youtube.com") || firstUrl.Contains("youtu.be"))
                            linkType = "youtube";
                        else if (firstUrl.Contains("twitter.com") || firstUrl.Contains("x.com"))
                            linkType = "twitter";
                        else if (firstUrl.Contains("tiktok.com"))
                            linkType = "tiktok";
                        else if (firstUrl.Contains("instagram.com"))
                            linkType = "instagram";
                        else if (firstUrl.Contains("facebook.com"))
                            linkType = "facebook";
                        else
                            linkType = "other";
                    }
                    else
                    {
                        isLink = false;
                    }
                }
                
                await _loggingService.LogMediaProcessingAsync(sender, mediaType, false, successful, extension, isLink, linkType, null, errors);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageQueue] Failed to log to Supabase: {ex.Message}");
            }
        }

        /// <summary>
        /// Simulates message processing (replace with actual processing logic)
        /// </summary>
        private async Task SimulateMessageProcessing(MediaMessage message)
        {
            // Simulate different processing times based on media type
            int processingTime = message.MediaType?.ToLower() switch
            {
                "image" => 500,
                "video" => 2000,
                "audio" => 1000,
                "document" => 800,
                _ => 300
            };

            await Task.Delay(processingTime);
            
            // Random chance of failure for testing error handling
            if (new Random().Next(100) < 5) // 5% chance of failure
            {
                throw new InvalidOperationException($"Simulated processing error for {message.Id}");
            }
        }

        /// <summary>
        /// Clears all processed message IDs to allow reprocessing
        /// </summary>
        public void ClearProcessedIds()
        {
            lock (_lockObject)
            {
                _processedIds.Clear();
                System.Diagnostics.Debug.WriteLine("[MessageQueue] Cleared processed IDs cache");
            }
        }

        /// <summary>
        /// Gets queue statistics
        /// </summary>
        public QueueStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new QueueStatistics
                {
                    QueuedCount = QueuedCount,
                    ProcessedCount = ProcessedCount,
                    FailedCount = FailedCount,
                    PendingCount = _pendingBatch.Count,
                    IsProcessing = IsProcessing,
                    ProcessedIdsCount = _processedIds.Count
                };
            }
        }

        /// <summary>
        /// Tests the Supabase logging connection
        /// </summary>
        public async Task<bool> TestLoggingConnectionAsync()
        {
            try
            {
                return await _loggingService.TestConnectionAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageQueue] Error testing logging connection: {ex.Message}");
                return false;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnQueueStatusChanged()
        {
            var args = new QueueStatusEventArgs
            {
                QueuedCount = QueuedCount,
                ProcessedCount = ProcessedCount,
                FailedCount = FailedCount,
                IsProcessing = IsProcessing
            };
            QueueStatusChanged?.Invoke(this, args);
        }

        public void Dispose()
        {
            try
            {
                IsProcessingEnabled = false;
                _batchProcessingTimer?.Dispose();
                _batchProcessingTimer = null;
                
                // Dispose logging service
                _loggingService?.Dispose();
                
                // Clear collections
                lock (_lockObject)
                {
                    _processedIds.Clear();
                    _pendingBatch.Clear();
                }
                
                // Clear UI collection on main thread
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _mediaItems.Clear();
                    });
                }
                
                System.Diagnostics.Debug.WriteLine("[MessageQueue] Disposed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageQueue] Error during disposal: {ex.Message}");
            }
        }
    }

    public class QueueStatusEventArgs : EventArgs
    {
        public int QueuedCount { get; set; }
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
        public bool IsProcessing { get; set; }
    }

    public class QueueStatistics
    {
        public int QueuedCount { get; set; }
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public bool IsProcessing { get; set; }
        public int ProcessedIdsCount { get; set; }
    }
}