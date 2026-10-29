using UrantijTwitchMusicPlayer.Work;
using UrantijTwitchMusicPlayer.Work.Links;

namespace UrantijTwitchMusicPlayer.Titch;

public class TitchMusicEvent(
    string twitchUserId,
    string twitchUserName,
    string? twitchDisplayName,
    string text,
    LinkInfo linkInfo)
    : BaseMusicEvent(linkInfo)
{
    public string TwitchUserId { get; set; } = twitchUserId;
    public string TwitchUserName { get; set; } = twitchUserName;
    public string? TwitchDisplayName { get; set; } = twitchDisplayName;
    public string Text { get; set; } = text;
}