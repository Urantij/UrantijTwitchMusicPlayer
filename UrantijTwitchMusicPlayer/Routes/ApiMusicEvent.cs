using UrantijTwitchMusicPlayer.Work;
using UrantijTwitchMusicPlayer.Work.Links;

namespace UrantijTwitchMusicPlayer.Routes;

public class ApiMusicEvent : BaseMusicEvent
{
    public string? BehalfTwitchUsername { get; }

    public ApiMusicEvent(LinkInfo linkInfo, string? behalfTwitchUsername) : base(linkInfo)
    {
        BehalfTwitchUsername = behalfTwitchUsername;
    }
}