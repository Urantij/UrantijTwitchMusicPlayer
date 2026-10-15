using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UrantijTwitchMusicPlayer.Shared.Data.Models;

public class RequestApiData
{
    [Key] [ForeignKey(nameof(Request))] public int RequestId { get; set; }
    public RequestModel Request { get; set; }

    [ForeignKey(nameof(BehalfUser))] public int BehalfUserId { get; set; }
    public TwitchUserModel BehalfUser { get; set; }
}