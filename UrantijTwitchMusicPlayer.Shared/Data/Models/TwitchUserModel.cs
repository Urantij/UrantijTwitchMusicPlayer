using System.ComponentModel.DataAnnotations;

namespace UrantijTwitchMusicPlayer.Shared.Data.Models;

public class TwitchUserModel
{
    [Key] public int Id { get; set; }

    public string TwitchId { get; set; }
    public string TwitchUsername { get; set; }

    public ICollection<RequestTwitchData> Datas { get; set; }
}