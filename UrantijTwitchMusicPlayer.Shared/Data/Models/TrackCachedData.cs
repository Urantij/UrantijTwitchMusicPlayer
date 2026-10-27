using System.ComponentModel.DataAnnotations;

namespace UrantijTwitchMusicPlayer.Shared.Data.Models;

public class TrackCachedData
{
    [Key] public int Id { get; set; }

    public string Link { get; set; }
    public double I { get; set; }
    public double Threshold { get; set; }
    public double Lra { get; set; }
    public double TruePeak { get; set; }

    public DateTimeOffset CreationDate { get; set; }
    public DateTimeOffset LastCheckDate { get; set; }
}