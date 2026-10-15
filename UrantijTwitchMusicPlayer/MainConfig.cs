using System.Text.Json.Serialization;

namespace UrantijTwitchMusicPlayer;

[JsonSerializable(typeof(MainConfig))]
internal partial class MainConfigSerializationContext : JsonSerializerContext;

public class MainConfig
{
    public string Channel { get; set; }

    public string RewardId { get; set; }

    public string FfmpegPath { get; set; } = "ffmpeg";

    public string MpvSavePath { get; set; } = "./mpvcache.txt";
    public bool AutoStartMpv { get; set; } = true;
    public bool TryMpvExistingSocket { get; set; } = false;

    public string[] AllowedAudioLocations { get; set; } =
    [
        "/home/punky/Music/nicotine/",
        "/home/punky/Music/karina"
    ];
}