using System.Collections.Generic;

namespace WAload.Models
{
    public class VideoSettings
    {
        public string VersionCommand { get; set; } = "-version";
        public string DurationCommand { get; set; } = "-i \"{input_path}\"";
        
        public VideoBlurSettings VideoBlurMp4 { get; set; } = new();
        public VideoBlurSettings VideoBlurMxf { get; set; } = new();
        public ImageBlurSettings ImageBlur { get; set; } = new();
        public ThumbnailSettings ThumbnailPrimary { get; set; } = new();
        public ThumbnailSettings ThumbnailFallback { get; set; } = new();
        public ProcessingSettings Processing { get; set; } = new();
    }

    public class VideoBlurSettings
    {
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class ImageBlurSettings
    {
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class ThumbnailSettings
    {
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class ProcessingSettings
    {
        public int BlurIntensity { get; set; } = 20;
        public int ImageBlurIntensity { get; set; } = 10;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int ThumbnailWidth { get; set; } = 320;
        public int Mp4Crf { get; set; } = 23;
        public string Mp4Preset { get; set; } = "medium";
        public string MxfBitrate { get; set; } = "50M";
        public int MxfFramerate { get; set; } = 25;
        public int MxfAudioSampleRate { get; set; } = 48000;
        public string MxfAudioCodec { get; set; } = "pcm_s24le";
    }
}





