using System.ComponentModel.DataAnnotations;

namespace UrantijTwitchMusicPlayer.Shared.Data.Models;

public class RequestModel
{
    [Key] public int Id { get; set; }

    public string TargetUrl { get; set; }
    public string TrackData { get; set; }

    public RequestTwitchData? TwitchData { get; set; }
    public RequestApiData? ApiData { get; set; }

    public DateTimeOffset RequestDate { get; set; }
    public DateTimeOffset RequestLastDate { get; set; }
}