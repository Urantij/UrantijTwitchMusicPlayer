namespace UrantijTwitchMusicPlayer.Work.Leaderboard;

public class LeaderboardUser(string name, double minutes, int requests)
{
    public string Name { get; } = name;
    public double Minutes { get; } = minutes;
    public int Requests { get; } = requests;
}

public class Leaderboard(LeaderboardUser[] users)
{
    public ICollection<LeaderboardUser> Users { get; } = users;
}