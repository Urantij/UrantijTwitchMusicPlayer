using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UrantijTwitchMusicPlayer.Shared.Data.Models;

public class RequestTwitchData
{
    [Key] [ForeignKey(nameof(Request))] public int RequestId { get; set; }
    public RequestModel Request { get; set; }

    public string Text { get; set; }

    [ForeignKey(nameof(User))] public int UserId { get; set; }
    public TwitchUserModel User { get; set; }
}