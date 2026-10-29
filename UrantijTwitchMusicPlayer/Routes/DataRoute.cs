using System.Text;
using Microsoft.AspNetCore.Mvc;
using UrantijTwitchMusicPlayer.Work.Leaderboard;

namespace UrantijTwitchMusicPlayer.Routes;

public class DataRoute
{
    public static async Task<IResult> GetLeaderboard(HttpContext httpContext,
        [FromServices] LeaderboardWorker leaderboardWorker,
        [FromServices] ILogger<DataRoute> logger)
    {
        // TODO бредик
        int max = Math.Max(int.Parse(httpContext.Request.Query["max"].FirstOrDefault() ?? "10"), 10);

        bool text = httpContext.Request.Query["text"].Any();

        if (text)
        {
            Leaderboard leaderboard = await leaderboardWorker.GetLeaderboardAsync(max);

            string response =
                string.Join(
                    '\n',
                    leaderboard.Users.Select((u, index) => $"{index + 1}. {u.Name} {u.Minutes} {u.Requests}")
                        .ToArray());

            return TypedResults.Content(response, "text/plain", Encoding.UTF8);
        }
        else
        {
            Leaderboard leaderboard = await leaderboardWorker.GetLeaderboardAsync(max);

            return TypedResults.Ok(leaderboard);
        }
    }
}