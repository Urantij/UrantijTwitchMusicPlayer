using System.ComponentModel.DataAnnotations;

namespace UrantijTwitchMusicPlayer.Shared.Data.Models;

public class TrackCachedData
{
    [Key] public int Id { get; set; }

    public string Link { get; set; }
    public double Lufs { get; set; }
    public DateTimeOffset CreationDate { get; set; }
    public DateTimeOffset LastCheckDate { get; set; }
}