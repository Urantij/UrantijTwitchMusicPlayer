using System.Text.Json.Serialization;

namespace UrantijTwitchMusicPlayer;

[JsonSerializable(typeof(MainConfig))]
internal partial class MainConfigSerializationContext : JsonSerializerContext;

public class MainConfig
{
    public string Channel { get; set; }

    public string RewardId { get; set; }

    public bool UseTwitchChat { get; set; } = true;

    public string? YoutubeProxyUrl { get; set; }
    public bool DownloadThumbnail { get; set; } = true;

    public string FfmpegPath { get; set; } = "ffmpeg";
    public bool DisplayFfmpegStats { get; set; } = false;

    public string MpvSavePath { get; set; } = "./mpvcache.txt";
    public bool AutoStartMpv { get; set; } = true;
    public bool TryMpvExistingSocket { get; set; } = false;
    public string MpvSocketPath { get; set; } = "/tmp/mpv-socket";

    public string AudioCacheLocation { get; set; } = "/home/punky/Music/karina/stream/";

    public string[] AudioSearchLocations { get; set; } =
    [
        "/home/punky/Music/karina/stream/",
        "/home/punky/Music/karina"
    ];

    public string[] AudioExtensions { get; set; } =
    [
        ".webm",
        ".mp4",
        ".mp3"
    ];

    public string[] AllowedAudioLocations { get; set; } =
    [
        "/home/punky/Music/nicotine/",
        "/home/punky/Music/karina"
    ];
}