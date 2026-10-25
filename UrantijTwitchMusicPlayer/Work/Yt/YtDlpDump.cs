using System.Text.Json.Serialization;

namespace UrantijTwitchMusicPlayer.Work.Yt;

public class YtDlpDump
{
    public class ThumbnailM
    {
        [JsonPropertyName("url")] public string Url { get; set; }
        [JsonPropertyName("preference")] public int Preference { get; set; }
        [JsonPropertyName("id")] public string Id { get; set; }
    }

    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("thumbnails")] public ThumbnailM[] Thumbnails { get; set; }

    [JsonPropertyName("thumbnail")] public string Thumbnail { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }

    [JsonPropertyName("duration")] public int Duration { get; set; }

    [JsonPropertyName("channel")] public string Channel { get; set; }

    /// <summary>
    /// 20170622
    /// </summary>
    [JsonPropertyName("upload_date")]
    public string UploadDate { get; set; }

    [JsonPropertyName("original_url")] public string OriginalUrl { get; set; }

    [JsonPropertyName("filesize")] public long Filesize { get; set; }

    [JsonPropertyName("ext")] public string Ext { get; set; }

    [JsonPropertyName("acodec")] public string ACodec { get; set; }
    [JsonPropertyName("container")] public string Container { get; set; }

    [JsonPropertyName("_filename")] public string UnderscoreFilename { get; set; }
    [JsonPropertyName("filename")] public string Filename { get; set; }

    //

    [JsonPropertyName("album")] public string? Album { get; set; }
    [JsonPropertyName("track")] public string? Track { get; set; }
    [JsonPropertyName("release_year")] public int? ReleaseYear { get; set; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
    [JsonPropertyName("artist")] public string? Artist { get; set; }
}