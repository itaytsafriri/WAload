using System;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WAload.Services
{
    public class SupabaseLoggingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _supabaseApiKey;
        private readonly string _macId;

        public SupabaseLoggingService()
        {
            _httpClient = new HttpClient();
            _supabaseUrl = "https://gddowcofpogcxtnqzxup.supabase.co";
            _supabaseApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImdkZG93Y29mcG9nY3h0bnF6eHVwIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTQ1ODE1ODEsImV4cCI6MjA3MDE1NzU4MX0.xsQp64onCAhIgLKOG0i3lZScy_fmQDibb3fqrwoXymg";
            
            // Set up headers
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseApiKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseApiKey}");
            _httpClient.DefaultRequestHeaders.Add("Prefer", "return=minimal");
            
            // Get MAC ID
            _macId = GetMacAddress();
        }

        private string GetMacAddress()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'");
                using var collection = searcher.Get();
                
                foreach (ManagementObject obj in collection)
                {
                    string[] addresses = (string[])obj["IPAddress"];
                    if (addresses != null && addresses.Length > 0)
                    {
                        string macAddress = obj["MACAddress"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(macAddress))
                        {
                            return macAddress.Replace(":", "").Replace("-", "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseLogging] Error getting MAC address: {ex.Message}");
            }
            
            return "unknown";
        }

        public async Task LogClientActivityAsync(string sender, string mediaType, string autoConverted, string successful, string extension, bool? isLink = null, string? linkType = null, bool? ytdlUsed = null, bool? wasSuccess = null, string? errors = null)
        {
            try
            {
                var logEntry = new
                {
                    mac_id = _macId,
                    sender = sender,
                    mediatype = mediaType,
                    autoconverted = autoConverted,
                    succsesful = successful,
                    ext = extension,
                    is_link = isLink,
                    link_type = linkType,
                    ytdl_used = ytdlUsed,
                    was_sucsess = wasSuccess,
                    errors = errors
                };

                var json = JsonConvert.SerializeObject(logEntry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/WAL%20Clients%20Log", content);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[SupabaseLogging] Successfully logged activity for sender: {sender}, mediaType: {mediaType}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseLogging] Failed to log activity. Status: {response.StatusCode}, Error: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseLogging] Exception while logging activity: {ex.Message}");
            }
        }

        public async Task LogMediaProcessingAsync(string sender, string mediaType, bool autoConverted, bool successful, string extension, bool? isLink = null, string? linkType = null, bool? ytdlUsed = null, string? errors = null)
        {
            await LogClientActivityAsync(
                sender,
                mediaType,
                autoConverted ? "yes" : "no",
                successful ? "yes" : "no",
                extension,
                isLink,
                linkType,
                ytdlUsed,
                successful, // Map successful to was_sucsess
                errors
            );
        }

        /// <summary>
        /// Test method to verify Supabase connection and logging
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var testEntry = new
                {
                    mac_id = _macId,
                    sender = "test",
                    mediatype = "test",
                    autoconverted = "no",
                    succsesful = "yes",
                    ext = "test",
                    is_link = false,
                    link_type = (string?)null,
                    ytdl_used = false,
                    was_sucsess = true,
                    errors = (string?)null
                };

                var json = JsonConvert.SerializeObject(testEntry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/WAL%20Clients%20Log", content);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("[SupabaseLogging] Test connection successful");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SupabaseLogging] Test connection failed. Status: {response.StatusCode}, Error: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseLogging] Test connection exception: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
