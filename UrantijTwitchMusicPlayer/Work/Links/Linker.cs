using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work.Links;

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
        if (searchString.StartsWith('/'))
        {
            string? location = _allowedAudioLocations.FirstOrDefault(l => searchString.StartsWith(l));

            if (location == null)
                throw new ForbiddenAudioFileLocationException();

            string path = Path.GetFullPath(searchString);

            if (!searchString.StartsWith(path))
                throw new ForbiddenAudioFileLocationException();

            if (!File.Exists(path))
                throw new AudioFileNotFoundException();

            return new LocalFileLinkInfo(path);
        }

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

        return null;
    }
}