using UrantijTwitchMusicPlayer.Data;

namespace UrantijTwitchMusicPlayer.Work.Leaderboard;

class YaSalsyaSuka(TimeSpan duration, int requests)
{
    public TimeSpan Duration { get; set; } = duration;
    public int Requests { get; set; } = requests;
}

public class LeaderboardWorker
{
    private readonly Database _database;

    public LeaderboardWorker(Database database)
    {
        _database = database;
    }

    public async Task<Leaderboard> GetLeaderboardAsync(int max, CancellationToken cancellationToken = default)
    {
        // TODO а может лучше сделашеь ебаную таблицу в котором будут эти значения а не пересчитывать всю информацию каждый ебаный раз, м?

        Dictionary<int, YaSalsyaSuka> dict = [];

        await _database.EnumerateAllRequestsWithDataWithoutUsersAsync(request =>
        {
            int? userId = request.TwitchData?.UserId ?? request.ApiData?.BehalfUserId;
            if (userId == null)
                return;

            TimeSpan duration = request.RequestLastDate - request.RequestDate;

            if (!dict.TryGetValue(userId.Value, out var user))
            {
                user = new YaSalsyaSuka(duration, 1);
                dict[userId.Value] = user;
                return;
            }

            user.Duration += duration;
            user.Requests++;
        }, cancellationToken);

        KeyValuePair<int, YaSalsyaSuka>[] toppers = dict
            .OrderByDescending(p => p.Value.Duration)
            .Take(max)
            .ToArray();

        List<ShortUserA> namesList = [];
        foreach (KeyValuePair<int, YaSalsyaSuka> topper in toppers)
        {
            ShortUserA? sua = await _database.GetUsernameFromIdAsync(topper.Key);

            if (sua == null)
                continue;

            namesList.Add(sua);
        }

        LeaderboardUser[] users = toppers.Select(t =>
        {
            ShortUserA? nameObj = namesList.FirstOrDefault(n => n.Id == t.Key);

            string name = nameObj?.Displayname ?? nameObj?.Username ?? $"Id {t.Key}";

            return new LeaderboardUser(name, t.Value.Duration.TotalMinutes, t.Value.Requests);
        }).ToArray();

        return new Leaderboard(users);
    }
}