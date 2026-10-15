using TwitchSimpleLib.Chat.Messages;
using UrantijTwitchMusicPlayer.Data;
using UrantijTwitchMusicPlayer.Routes;
using UrantijTwitchMusicPlayer.Shared.Data.Models;
using UrantijTwitchMusicPlayer.Titch;

namespace UrantijTwitchMusicPlayer.Work;

public abstract class BaseMusicEvent
{
    public LinkInfo LinkInfo { get; }

    protected BaseMusicEvent(LinkInfo linkInfo)
    {
        LinkInfo = linkInfo;
    }
}

class RequestInProgress
{
    public readonly CancellationTokenSource Cts = new();

    public BaseMusicEvent Ev { get; }

    public int? RequestId { get; set; }

    public RequestInProgress(BaseMusicEvent ev)
    {
        Ev = ev;
    }
}

public class MainWorker : IHostedService
{
    private readonly TitchClient _titch;
    private readonly MpvWorker _mpv;
    private readonly YtDlpWorker _ytlp;
    private readonly FfmpegWorker _ffmpeg;
    private readonly CacheWorker _cache;
    private readonly Linker _linker;
    private readonly Database _database;

    private readonly ILogger _logger;

    private readonly Lock _lock = new();
    private RequestInProgress? _inProgress = null;

    public double? CurrentTrackLufs { get; private set; }

    public MainWorker(TitchClient titch, MpvWorker mpv, YtDlpWorker ytlp, FfmpegWorker ffmpeg, CacheWorker cache,
        Linker linker,
        Database database, ILogger<MainWorker> logger)
    {
        _titch = titch;
        _mpv = mpv;
        _ytlp = ytlp;
        _ffmpeg = ffmpeg;
        _cache = cache;
        _linker = linker;
        _database = database;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _titch.RewardMessageReceived += TitchOnRewardMessageReceived;

        Task.Run(async () => { await LoopCheckAsync(cancellationToken); }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _titch.RewardMessageReceived -= TitchOnRewardMessageReceived;

        return Task.CompletedTask;
    }

    private async Task LoopCheckAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            int requestId;
            lock (_lock)
            {
                if (_inProgress?.RequestId == null)
                    continue;

                requestId = _inProgress.RequestId.Value;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;

            await _database.UpdateRequestLastDateAsync(requestId, now);
        }
    }

    private void TitchOnRewardMessageReceived(TwitchPrivateMessage msg)
    {
        Task.Run(async () =>
        {
            LinkInfo? linkInfo = null;
            foreach (string word in msg.text.Split(' '))
            {
                linkInfo = _linker.MaybeLink(word);

                if (linkInfo != null)
                    break;
            }

            if (linkInfo == null)
            {
                _logger.LogWarning("Награда была заюзана, а ссылка была не найдена");
                return;
            }

            TitchMusicEvent ev = new(msg.userId, msg.username, msg.text, linkInfo);
            await AddRewardAsync(ev);
        });
    }

    public async Task AddRewardAsync(BaseMusicEvent ev)
    {
        RequestInProgress current;
        RequestInProgress? endingRequest = null;
        lock (_lock)
        {
            if (_inProgress != null)
            {
                _inProgress.Cts.Cancel();
                _inProgress.Cts.Dispose();

                endingRequest = _inProgress;
            }

            current = _inProgress = new RequestInProgress(ev);
        }

        if (endingRequest?.RequestId != null)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await _database.UpdateRequestLastDateAsync(endingRequest.RequestId.Value, now);
        }

        try
        {
            await RunRewardAsync(current);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "обработку не йес йес");
        }
    }

    private async Task RunRewardAsync(RequestInProgress request)
    {
        int? twitchUserDbId = null;
        if (request.Ev is ApiMusicEvent { BehalfTwitchUsername: not null } apiMusicEvent)
        {
            ChatUserData? chatUserData = _titch.FindUserByUsername(apiMusicEvent.BehalfTwitchUsername);

            if (chatUserData == null)
            {
                twitchUserDbId =
                    await _database.GetTwitchUserIdByTwitchUsernameAsync(apiMusicEvent.BehalfTwitchUsername);
            }

            if (twitchUserDbId == null)
            {
                throw new Exception($"Не нашли юзера с именем {apiMusicEvent.BehalfTwitchUsername}");
            }
        }

        string? filePath = null;

        if (request.Ev.LinkInfo is YoutubeLinkInfo or SoundcloudLinkInfo)
        {
            filePath = _cache.Find(request.Ev.LinkInfo);

            if (filePath == null)
            {
                filePath = await _ytlp.DownloadAsync(request.Ev.LinkInfo.Link, request.Cts.Token);
            }
        }
        else if (request.Ev.LinkInfo is LocalFileLinkInfo)
        {
            filePath = request.Ev.LinkInfo.Link;
        }
        else
        {
            throw new Exception($"че ты накодил епт {request.Ev.LinkInfo.GetType().Name}");
        }

        double lufs;
        {
            string genericLink = _linker.MakeGenericLink(request.Ev.LinkInfo);
            TrackCachedData? cache = await _database.FindTrackCacheAsync(genericLink);

            if (cache != null)
            {
                _logger.LogInformation("Нашли луфс в бд");
                lufs = cache.Lufs;

                await _database.UpdateTrackCacheDateAsync(cache.Id, DateTimeOffset.UtcNow);
            }
            else
            {
                _logger.LogInformation("Вычисляем луфсы...");
                lufs = await _ffmpeg.LufsAsync(filePath, request.Cts.Token);

                await _database.AddTrackCacheAsync(genericLink, lufs, DateTimeOffset.UtcNow);
            }

            CurrentTrackLufs = lufs;
        }

        double volume = _mpv.CalculateVolumePercentForLufs(lufs);

        string trackData = Path.GetFileNameWithoutExtension(filePath);

        {
            if (request.Ev is TitchMusicEvent titch)
            {
                _logger.LogInformation("Ставим трек {data} ({lufs:N2}) от {name}", trackData, lufs,
                    titch.TwitchUserName);
            }
            else
            {
                _logger.LogInformation("Ставим трек {data} ({lufs:N2})", trackData, lufs);
            }
        }

        await _mpv.SetPlayerThingAsync(filePath, volume);

        int requestId;
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (request.Ev is TitchMusicEvent titch)
            {
                int? userId = await _database.GetTwitchUserIdAsync(titch.TwitchUserId);

                if (userId == null)
                {
                    userId = await _database.AddTwitchUserAsync(titch.TwitchUserId, titch.TwitchUserName);
                }

                requestId =
                    await _database.AddTwitchRequestAsync(userId.Value, titch.Text, request.Ev.LinkInfo.Link, trackData,
                        now);
            }
            else if (request.Ev is ApiMusicEvent api)
            {
                if (api.BehalfTwitchUsername != null)
                {
                    requestId =
                        await _database.AddApiRequestAsync(request.Ev.LinkInfo.Link, trackData, now,
                            twitchUserDbId.Value);
                }
                else
                {
                    requestId =
                        await _database.AddApiRequestAsync(request.Ev.LinkInfo.Link, trackData, now);
                }
            }
            else
            {
                throw new Exception($"че ты накодил епт {request.Ev.LinkInfo.GetType().Name}");
            }
        }

        lock (_lock)
        {
            if (request == _inProgress)
            {
                request.RequestId = requestId;
            }
        }
    }
}