using System;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using WAload.Models;

namespace WAload.Services
{
    public class VideoSettingsService
    {
        private readonly string _settingsPath;
        private readonly string _defaultSettingsPath;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public VideoSettingsService()
        {
            var assemblyDirectory = AppContext.BaseDirectory;
            _settingsPath = Path.Combine(assemblyDirectory, "video-settings.yaml");
            _defaultSettingsPath = Path.Combine(assemblyDirectory, "video-settings-default.yaml");
            
            _serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        /// <summary>
        /// Loads video settings from the YAML file
        /// </summary>
        /// <returns>VideoSettings object or null if loading fails</returns>
        public VideoSettings? LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoSettings] Settings file not found at {_settingsPath}, creating default");
                    CreateDefaultSettings();
                }

                var yamlContent = File.ReadAllText(_settingsPath, Encoding.UTF8);
                var settings = _deserializer.Deserialize<VideoSettings>(yamlContent);
                
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Successfully loaded settings from {_settingsPath}");
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Error loading settings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves video settings to the YAML file
        /// </summary>
        /// <param name="settings">Settings to save</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveSettings(VideoSettings settings)
        {
            try
            {
                var yamlContent = _serializer.Serialize(settings);
                File.WriteAllText(_settingsPath, yamlContent, Encoding.UTF8);
                
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Successfully saved settings to {_settingsPath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Error saving settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets video settings to default values
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool ResetToDefault()
        {
            try
            {
                var defaultSettings = GetDefaultSettings();
                return SaveSettings(defaultSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Error resetting to default: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates the default settings file
        /// </summary>
        private void CreateDefaultSettings()
        {
            try
            {
                var defaultSettings = GetDefaultSettings();
                SaveSettings(defaultSettings);
                
                // Also save as default backup
                var yamlContent = _serializer.Serialize(defaultSettings);
                File.WriteAllText(_defaultSettingsPath, yamlContent, Encoding.UTF8);
                
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Created default settings file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoSettings] Error creating default settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the default video settings
        /// </summary>
        /// <returns>Default VideoSettings object</returns>
        private VideoSettings GetDefaultSettings()
        {
            return new VideoSettings
            {
                VersionCommand = "-version",
                DurationCommand = "-i \"{input_path}\"",
                
                VideoBlurMp4 = new VideoBlurSettings
                {
                    Command = "-y -i \"{input_path}\" -filter_complex \"[0:v]scale=1920:1080:force_original_aspect_ratio=increase,gblur=sigma=20,crop=1920:1080[bg];[0:v]scale=1920:1080:force_original_aspect_ratio=decrease[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2:format=auto,format=yuv420p\" -c:v libx264 -preset medium -crf 23 -movflags +faststart \"{output_path}\"",
                    Description = "Converts videos to 16:9 aspect ratio with blurred background for social media"
                },
                
                VideoBlurMxf = new VideoBlurSettings
                {
                    Command = "-y -i \"{input_path}\" -filter_complex \"[0:v]scale=1920:1080:force_original_aspect_ratio=increase,gblur=sigma=20,crop=1920:1080,format=yuv422p[bg];[0:v]scale=1920:1080:force_original_aspect_ratio=decrease,format=yuv422p[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2:format=auto,format=yuv422p,fps=25,fieldorder=tff[vid];[0:a]channelsplit=channel_layout=stereo[left][right];[left]aresample=async=1:first_pts=0[leftfix];[right]aresample=async=1:first_pts=0[rightfix]\" -map \"[vid]\" -map \"[leftfix]\" -map \"[rightfix]\" -c:v mpeg2video -r 25 -pix_fmt yuv422p -b:v 50M -minrate 50M -maxrate 50M -bufsize 17825792 -flags +ildct+ilme -g 12 -bf 2 -color_range tv -c:a pcm_s24le -ar 48000 -metadata:s:a:0 \"track_name=Track 2\" -metadata:s:a:1 \"track_name=Track 3\" -metadata \"company_name=Open Media App\" -metadata \"application_platform=Windows 10\" -timecode 00:00:00:00 -f mxf -muxpreload 0 -muxdelay 0 -shortest \"{output_path}\"",
                    Description = "Creates MXF files compatible with Avid editing software"
                },
                
                ImageBlur = new ImageBlurSettings
                {
                    Command = "-i \"{input_path}\" -filter_complex \"[0:v]scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,boxblur=10[bg];[0:v]scale=-1:1080,setsar=1[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2\" -frames:v 1 -update 1 \"{output_path}\"",
                    Description = "Converts images to 16:9 aspect ratio with blurred background"
                },
                
                ThumbnailPrimary = new ThumbnailSettings
                {
                    Command = "-y -i \"{input_path}\" -vframes 1 -ss {time_position} -vf \"scale=320:-1:flags=lanczos\" -q:v 2 \"{output_path}\"",
                    Description = "Extracts a single frame from video at specific time position for thumbnail"
                },
                
                ThumbnailFallback = new ThumbnailSettings
                {
                    Command = "-y -i \"{input_path}\" -vframes 1 -vf \"scale=320:-1:flags=lanczos\" -q:v 2 \"{output_path}\"",
                    Description = "Fallback method - extracts first frame without seeking to specific time"
                },
                
                Processing = new ProcessingSettings
                {
                    BlurIntensity = 20,
                    ImageBlurIntensity = 10,
                    Width = 1920,
                    Height = 1080,
                    ThumbnailWidth = 320,
                    Mp4Crf = 23,
                    Mp4Preset = "medium",
                    MxfBitrate = "50M",
                    MxfFramerate = 25,
                    MxfAudioSampleRate = 48000,
                    MxfAudioCodec = "pcm_s24le"
                }
            };
        }

        /// <summary>
        /// Gets the path to the settings file
        /// </summary>
        /// <returns>Path to the settings file</returns>
        public string GetSettingsPath()
        {
            return _settingsPath;
        }

        /// <summary>
        /// Checks if the settings file exists
        /// </summary>
        /// <returns>True if settings file exists</returns>
        public bool SettingsFileExists()
        {
            return File.Exists(_settingsPath);
        }
    }
}
