using Microsoft.EntityFrameworkCore;
using UrantijTwitchMusicPlayer.Shared.Data;
using UrantijTwitchMusicPlayer.Shared.Data.Models;
using UrantijTwitchMusicPlayer.Work.Ffmpeg;

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

    public async Task<int> AddTwitchUserAsync(string twitchUserId, string twitchUserName, string? twitchDisplayName)
    {
        await using MyContext context = await CreateContextAsync();

        TwitchUserModel twitchUser = new()
        {
            TwitchId = twitchUserId,
            TwitchUsername = twitchUserName,
            TwitchDisplayname = twitchDisplayName
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

    public async Task<TrackCachedData> AddTrackCacheAsync(string link, ILufsAnalyze analyze, DateTimeOffset date)
    {
        await using MyContext context = await CreateContextAsync();

        TrackCachedData data = new()
        {
            Link = link,
            I = analyze.GetI(),
            Threshold = analyze.GetThreshold(),
            Lra = analyze.GetLra(),
            TruePeak = analyze.GetTruePeak(),
            CreationDate = date,
            LastCheckDate = date
        };

        context.TrackCache.Add(data);

        await context.SaveChangesAsync();

        return data;
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

    public async Task EnumerateAllRequestsWithDataWithoutUsersAsync(Action<RequestModel> action,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync();

        // IAsyncEnumerable<TwitchUserModel> query = context.TwitchUsers.Include(u => u.TwitchDatas)
        //     .ThenInclude(d => d.Request)
        //     .Include(u => u.ApiDatas)
        //     .ThenInclude(d => d.Request)
        //     .OrderBy(u => u.Id)
        //     .AsNoTrackingWithIdentityResolution()
        //     .AsAsyncEnumerable();

        IAsyncEnumerable<RequestModel> requests = context.Requests
            .Include(r => r.ApiData)
            // .ThenInclude(a => a.BehalfUser)
            .Include(r => r.TwitchData)
            // .ThenInclude(d => d.User)
            .OrderBy(r => r.Id)
            // .AsNoTrackingWithIdentityResolution() я хызы мне каж не надо. если бы всё разом грузил наверное да. а так похуй
            .AsNoTracking()
            .AsAsyncEnumerable();

        await foreach (RequestModel request in requests)
        {
            action(request);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public async Task<ShortUserA[]> GetUsernamesFromIdsAsync(int[] ids)
    {
        await using var context = await CreateContextAsync();

        int[] a = ids;

        return await context.TwitchUsers
            .Where(u => a.Any(id => id == u.Id)) // Contains срал мне на голову
            .AsNoTracking()
            .Select(u => new ShortUserA(u.Id, u.TwitchUsername, u.TwitchDisplayname))
            .ToArrayAsync();
    }
}