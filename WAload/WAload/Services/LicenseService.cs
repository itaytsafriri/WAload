using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace WAload.Services
{
    public class LicenseService
    {
        private const string LicenseFileName = "waload_license.key";
        private const string MachineIdFileName = "waload_machine.id";
        private static readonly string LicenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName);
        private static readonly string MachineIdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MachineIdFileName);

        public class LicenseData
        {
            public string MachineId { get; set; } = string.Empty;
            public DateTime ExpiryDate { get; set; }
            public string[] Features { get; set; } = Array.Empty<string>();
            public DateTime GeneratedDate { get; set; }
        }

        public class LicenseValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public LicenseData? LicenseData { get; set; }
            public string[] AvailableFeatures { get; set; } = Array.Empty<string>();
        }

        public LicenseValidationResult ValidateLicense()
        {
            try
            {
                // Add timeout to prevent hanging
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                var validationTask = Task.Run(() => ValidateLicenseInternal());
                
                if (Task.WhenAny(validationTask, timeoutTask).Result == timeoutTask)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = "License validation timed out. Please try again."
                    };
                }
                
                return validationTask.Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error validating license: {ex.Message}");
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = $"License validation error: {ex.Message}"
                };
            }
        }

        private LicenseValidationResult ValidateLicenseInternal()
        {
            // Check if license file exists
            if (!File.Exists(LicenseFilePath))
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "License file not found. Please enter your license key."
                };
            }

            // Read license key
            var licenseKey = File.ReadAllText(LicenseFilePath).Trim();
            if (string.IsNullOrEmpty(licenseKey))
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "License file is empty. Please enter a valid license key."
                };
            }

            // Decrypt and validate license
            var licenseData = DecryptLicenseKey(licenseKey);
            if (licenseData == null)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "Invalid license key format."
                };
            }

            // Check if license has expired
            if (DateTime.Now > licenseData.ExpiryDate)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = $"License has expired on {licenseData.ExpiryDate:yyyy-MM-dd}."
                };
            }

            // Validate machine ID
            var currentMachineId = GetCurrentMachineId();
            if (licenseData.MachineId != currentMachineId)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "License is not valid for this machine."
                };
            }

            return new LicenseValidationResult
            {
                IsValid = true,
                Message = $"License valid until {licenseData.ExpiryDate:yyyy-MM-dd}",
                LicenseData = licenseData,
                AvailableFeatures = licenseData.Features
            };
        }

        public bool SaveLicenseKey(string licenseKey)
        {
            try
            {
                // Validate the license key first
                var licenseData = DecryptLicenseKey(licenseKey);
                if (licenseData == null)
                {
                    return false;
                }

                // Save the license key
                File.WriteAllText(LicenseFilePath, licenseKey);
                System.Diagnostics.Debug.WriteLine("[LicenseService] License key saved successfully.");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error saving license key: {ex.Message}");
                return false;
            }
        }

        public string GetCurrentMachineId()
        {
            try
            {
                // Add timeout to prevent hanging
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
                var machineIdTask = Task.Run(() => GetCurrentMachineIdInternal());
                
                if (Task.WhenAny(machineIdTask, timeoutTask).Result == timeoutTask)
                {
                    System.Diagnostics.Debug.WriteLine("[LicenseService] Machine ID generation timed out, using fallback");
                    return "TIMEOUT_MACHINE_ID";
                }
                
                return machineIdTask.Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error getting machine ID: {ex.Message}");
                return "UNKNOWN_MACHINE";
            }
        }

        private string GetCurrentMachineIdInternal()
        {
            // Check if we have a cached machine ID
            if (File.Exists(MachineIdFilePath))
            {
                return File.ReadAllText(MachineIdFilePath).Trim();
            }

            // Generate new machine ID
            var machineId = GenerateMachineId();
            File.WriteAllText(MachineIdFilePath, machineId);
            return machineId;
        }

        private string GenerateMachineId()
        {
            var sb = new StringBuilder();
            
            // Use more reliable hardware identifiers that don't require WMI
            try
            {
                // Get computer name
                sb.Append(Environment.MachineName);
                
                // Get user name
                sb.Append(Environment.UserName);
                
                // Get OS version
                sb.Append(Environment.OSVersion.ToString());
                
                // Get processor count
                sb.Append(Environment.ProcessorCount.ToString());
                
                // Get system directory
                sb.Append(Environment.SystemDirectory);
                
                // Get working set (memory)
                sb.Append(Environment.WorkingSet.ToString());
                
                // Get current directory
                sb.Append(Environment.CurrentDirectory);
            }
            catch
            {
                sb.Append("FALLBACK_ID");
            }

            // Create a hash of the combined system info
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return Convert.ToBase64String(hash).Substring(0, 32).Replace("/", "_").Replace("+", "-");
            }
        }

        private LicenseData? DecryptLicenseKey(string licenseKey)
        {
            try
            {
                // Restore base64 characters
                var encryptedData = licenseKey.Replace("_", "/").Replace("-", "+");
                var encryptedBytes = Convert.FromBase64String(encryptedData);

                using (var aes = Aes.Create())
                {
                    // Ensure exactly 16 bytes for AES-128
                    var keyBytes = Encoding.UTF8.GetBytes("WALoadKeyGen2024!!");
                    if (keyBytes.Length < 16)
                    {
                        // Pad with zeros if too short
                        var paddedKey = new byte[16];
                        Array.Copy(keyBytes, paddedKey, keyBytes.Length);
                        keyBytes = paddedKey;
                    }
                    else if (keyBytes.Length > 16)
                    {
                        // Truncate if too long
                        keyBytes = keyBytes.Take(16).ToArray();
                    }
                    
                    aes.Key = keyBytes;
                    aes.IV = new byte[16]; // Zero IV for simplicity

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(encryptedBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        var json = sr.ReadToEnd();
                        return JsonConvert.DeserializeObject<LicenseData>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error decrypting license key: {ex.Message}");
                return null;
            }
        }

        public bool HasFeature(string feature)
        {
            var result = ValidateLicense();
            if (!result.IsValid)
                return false;

            return result.AvailableFeatures.Any(f => string.Equals(f, feature, StringComparison.OrdinalIgnoreCase));
        }

        public void ClearLicense()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                }
                System.Diagnostics.Debug.WriteLine("[LicenseService] License cleared.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error clearing license: {ex.Message}");
            }
        }
    }
} 