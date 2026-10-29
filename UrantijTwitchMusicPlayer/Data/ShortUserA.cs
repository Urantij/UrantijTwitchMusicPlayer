namespace UrantijTwitchMusicPlayer.Data;

public class ShortUserA(int id, string username, string? displayname)
{
    public int Id { get; } = id;
    public string Username { get; } = username;
    public string? Displayname { get; } = displayname;
}