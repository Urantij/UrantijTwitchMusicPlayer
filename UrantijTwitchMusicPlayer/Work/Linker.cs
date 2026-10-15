using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work;

public abstract class LinkInfo
{
    public string Link { get; }

    protected LinkInfo(string link)
    {
        Link = link;
    }
}

public class YoutubeLinkInfo : LinkInfo
{
    public string Id { get; }

    public YoutubeLinkInfo(string link, string id) : base(link)
    {
        Id = id;
    }
}

public class SoundcloudLinkInfo : LinkInfo
{
    public string Author { get; }
    public string Track { get; }

    public SoundcloudLinkInfo(string link, string author, string track) : base(link)
    {
        Author = author;
        Track = track;
    }
}

public class LocalFileLinkInfo : LinkInfo
{
    public LocalFileLinkInfo(string link) : base(link)
    {
    }
}

public class Linker
{
    // TODO ну не конвертится блин
    private static readonly Regex Youtube1Regex =
        new(@"(https:\/\/){0,1}(www\.){0,1}(music\.){0,1}youtube\.com\/watch\?v=(?<id>.+?)(&|$)",
            RegexOptions.Compiled);

    private static readonly Regex Youtube2Regex =
        new(@"(https:\/\/){0,1}(www\.){0,1}youtu\.be\/(?<id>.+?)(\?|$)", RegexOptions.Compiled);

    private static readonly Regex SoundcloudRegex =
        new(@"(https:\/\/){0,1}(www\.){0,1}soundcloud\.com\/(?<author>.+)/(?<track>.+?)(\?|$)", RegexOptions.Compiled);

    private readonly string[] _allowedAudioLocations;

    public Linker(IOptions<MainConfig> mainOptions)
    {
        _allowedAudioLocations = mainOptions.Value.AllowedAudioLocations;
    }

    public string MakeGenericLink(LinkInfo link)
    {
        // типа ссылки на ютуб приводить к одну виду.

        if (link is YoutubeLinkInfo youtube)
        {
            return $"https://youtu.be/{youtube.Id}";
        }

        return link.Link;
    }

    public LinkInfo? MaybeLink(string searchString)
    {
        var match = Youtube1Regex.Match(searchString);
        if (match.Success)
        {
            return new YoutubeLinkInfo($"https://youtu.be/{match.Groups["id"]}", match.Groups["id"].Value);
        }

        match = Youtube2Regex.Match(searchString);
        if (match.Success)
        {
            return new YoutubeLinkInfo($"https://youtu.be/{match.Groups["id"]}", match.Groups["id"].Value);
        }

        match = SoundcloudRegex.Match(searchString);
        if (match.Success)
        {
            return new SoundcloudLinkInfo($"https://soundcloud.com/{match.Groups["author"]}/{match.Groups["track"]}",
                match.Groups["author"].Value, match.Groups["track"].Value);
        }

        foreach (string location in _allowedAudioLocations)
        {
            if (!searchString.StartsWith(location)) continue;

            string path = Path.GetFullPath(searchString);

            if (!searchString.StartsWith(path)) continue;

            if (!File.Exists(path))
                continue;

            return new LocalFileLinkInfo(path);
        }

        return null;
    }
}