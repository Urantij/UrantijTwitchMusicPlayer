using Microsoft.EntityFrameworkCore;
using UrantijTwitchMusicPlayer.Shared.Data;
using UrantijTwitchMusicPlayer.Shared.Data.Models;

namespace UrantijTwitchMusicPlayer.Data;

public class Database
{
    private readonly IDbContextFactory<MyContext> _factory;

    public Database(IDbContextFactory<MyContext> factory)
    {
        _factory = factory;
    }

    public Task<MyContext> CreateContextAsync()
    {
        return _factory.CreateDbContextAsync();
    }

    public async Task<int?> GetTwitchUserIdAsync(string twitchUserId)
    {
        await using MyContext context = await CreateContextAsync();

        string a = twitchUserId;

        return await context.TwitchUsers
            .Where(u => u.TwitchId == a)
            .AsNoTracking()
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<int?> GetTwitchUserIdByTwitchUsernameAsync(string twitchUsername)
    {
        await using MyContext context = await CreateContextAsync();

        string a = twitchUsername;

        return await context.TwitchUsers
            .Where(u => u.TwitchUsername == a)
            .AsNoTracking()
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> AddTwitchUserAsync(string twitchUserId, string twitchUserName)
    {
        await using MyContext context = await CreateContextAsync();

        TwitchUserModel twitchUser = new()
        {
            TwitchId = twitchUserId,
            TwitchUsername = twitchUserName
        };

        context.TwitchUsers.Add(twitchUser);

        await context.SaveChangesAsync();

        return twitchUser.Id;
    }

    public async Task<int> AddTwitchRequestAsync(int userId, string text, string targetUrl, string trackData,
        DateTimeOffset date)
    {
        await using MyContext context = await CreateContextAsync();

        RequestModel request = new()
        {
            TargetUrl = targetUrl,
            TrackData = trackData,
            RequestDate = date,
            RequestLastDate = date,
            TwitchData = new RequestTwitchData()
            {
                Text = text,
                UserId = userId
            }
        };

        context.Requests.Add(request);

        await context.SaveChangesAsync();

        return request.Id;
    }

    public async Task<int> AddApiRequestAsync(string targetUrl, string trackData, DateTimeOffset date)
    {
        await using MyContext context = await CreateContextAsync();

        RequestModel request = new()
        {
            TargetUrl = targetUrl,
            TrackData = trackData,
            RequestDate = date,
            RequestLastDate = date
        };

        context.Requests.Add(request);

        await context.SaveChangesAsync();

        return request.Id;
    }

    public async Task<int> AddApiRequestAsync(string targetUrl, string trackData, DateTimeOffset date, int userId)
    {
        await using MyContext context = await CreateContextAsync();

        RequestModel request = new()
        {
            TargetUrl = targetUrl,
            TrackData = trackData,
            RequestDate = date,
            RequestLastDate = date,
            ApiData = new RequestApiData()
            {
                BehalfUserId = userId
            }
        };

        context.Requests.Add(request);

        await context.SaveChangesAsync();

        return request.Id;
    }

    public async Task UpdateRequestLastDateAsync(int requestId, DateTimeOffset newLastDate)
    {
        await using MyContext context = await CreateContextAsync();

        RequestModel request = new()
        {
            Id = requestId
        };

        context.Requests.Attach(request);

        DateTimeOffset d = newLastDate;
        request.RequestLastDate = d;

        await context.SaveChangesAsync();
    }

    public async Task<TrackCachedData?> FindTrackCacheAsync(string link)
    {
        await using MyContext context = await CreateContextAsync();

        string a = link;

        return await context.TrackCache
            .Where(u => u.Link == a)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task AddTrackCacheAsync(string link, double lufs, DateTimeOffset date)
    {
        await using MyContext context = await CreateContextAsync();

        TrackCachedData data = new()
        {
            Link = link,
            Lufs = lufs,
            CreationDate = date,
            LastCheckDate = date
        };

        context.TrackCache.Add(data);

        await context.SaveChangesAsync();
    }

    public async Task UpdateTrackCacheDateAsync(int id, DateTimeOffset lastDate)
    {
        await using MyContext context = await CreateContextAsync();

        TrackCachedData data = new()
        {
            Id = id
        };

        context.TrackCache.Attach(data);

        DateTimeOffset d = lastDate;
        data.LastCheckDate = d;

        await context.SaveChangesAsync();
    }
}