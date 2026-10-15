using UrantijTwitchMusicPlayer.Work;

namespace UrantijTwitchMusicPlayer.Titch;

public class TitchMusicEvent(string twitchUserId, string twitchUserName, string text, LinkInfo linkInfo)
    : BaseMusicEvent(linkInfo)
{
    public string TwitchUserId { get; set; } = twitchUserId;
    public string TwitchUserName { get; set; } = twitchUserName;
    public string Text { get; set; } = text;
}