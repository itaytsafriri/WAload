using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WAload.Models;

namespace WAload.Services
{
    public interface IWhatsAppService
    {
        event EventHandler<string>? QrCodeReceived;
        event EventHandler<bool>? ConnectionStatusChanged;
        event EventHandler<string>? UserNameReceived;
        event EventHandler<List<WhatsGroup>>? GroupsUpdated;
        event EventHandler<MediaMessage>? MediaMessageReceived;
        event EventHandler<bool>? MonitoringStatusChanged;

        Task InitializeAsync();
        Task GetGroupsAsync();
        Task StartMonitoringAsync(string groupId);
        Task StopMonitoringAsync();
        Task LogoutAsync();
        Task DisposeAsync();

        bool IsConnected { get; }
        bool IsMonitoring { get; }
    }
} 