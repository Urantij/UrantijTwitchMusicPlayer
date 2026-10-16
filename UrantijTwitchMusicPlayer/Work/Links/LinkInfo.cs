namespace UrantijTwitchMusicPlayer.Work.Links;

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