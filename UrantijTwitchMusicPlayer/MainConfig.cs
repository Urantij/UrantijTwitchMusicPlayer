using System.Text.Json.Serialization;
using UrantijTwitchMusicPlayer.Work.Ffmpeg;

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
    public long DownloadFileMaxPermittedSize { get; set; } = 1024 * 1024 * 100;

    public string FfmpegPath { get; set; } = "ffmpeg";
    public bool DisplayFfmpegStats { get; set; } = false;

    public string MpvSavePath { get; set; } = "./mpvcache.txt";
    public bool AutoStartMpv { get; set; } = true;
    public bool TryMpvExistingSocket { get; set; } = false;
    public string MpvSocketPath { get; set; } = "/tmp/mpv-socket";
    public double MpvTargetI { get; set; } = -14;
    public double MpvTargetTruePeak { get; set; } = -1;
    public double MpvTargetTwoPassLra { get; set; } = 7;
    public double MpvTargetOnePasLra { get; set; } = 14;

    public LufzAnalyzeType MpvAnalyzeType { get; set; } = LufzAnalyzeType.Loudnorm;

    // Предисловие. Спасибо моему дорогому другу Киселю за моральную поддержку, пиздюли и работу моим калькулятором...
    // 3 мегабайта обрабатывалось 13.6 секунд, 1 мегабайт это 4.53 секунд на 1 мегабайт.
    // но ебур128 быстрее нахуй в 5 раз, что тоже кабутабы немаловажно. гугл говорил юзать лауднорм, но мне впадлу ждать.
    public long MpvLargeFileThreshold { get; set; } = 1024 * 1024 * 12;
    public long MpvMinFileSize { get; set; } = 1024 * 100;

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