using TwitchSimpleLib.Chat.Messages;
using UrantijTwitchMusicPlayer.Data;
using UrantijTwitchMusicPlayer.Routes;
using UrantijTwitchMusicPlayer.Titch;
using UrantijTwitchMusicPlayer.Work.Links;

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
    private readonly Piper _piper;
    private readonly CacheWorker _cache;
    private readonly Linker _linker;
    private readonly Database _database;

    private readonly ILogger _logger;

    private readonly Lock _lock = new();
    private RequestInProgress? _inProgress = null;

    public double? CurrentTrackLufs { get; private set; }

    public MainWorker(TitchClient titch, MpvWorker mpv, Piper piper, CacheWorker cache,
        Linker linker,
        Database database, ILogger<MainWorker> logger)
    {
        _titch = titch;
        _mpv = mpv;
        _piper = piper;
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
                try
                {
                    linkInfo = _linker.MaybeLink(word);
                }
                catch
                {
                    continue;
                }

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

        PipingController? piping = null;
        string? filePath = null;

        if (request.Ev.LinkInfo is YoutubeLinkInfo or SoundcloudLinkInfo)
        {
            filePath = _cache.Find(request.Ev.LinkInfo);

            if (filePath == null)
            {
                // TODO короч кансел тут может оставить временный файл не удалённым
                piping = await _piper.PipeAsync(request.Ev.LinkInfo.Link, request.Cts.Token);
                await piping.FfmpegTask.StartingTask;
                await Task.Delay(TimeSpan.FromMilliseconds(250), request.Cts.Token);
                filePath = piping.TmpFilePath;
            }
        }
        else if (request.Ev.LinkInfo is LocalFileLinkInfo)
        {
            filePath = request.Ev.LinkInfo.Link;
        }
        else
        {
            // TODO убрать всё говно тут
            throw new Exception($"че ты накодил епт {request.Ev.LinkInfo.GetType().Name}");
        }

        string trackData = Path.GetFileNameWithoutExtension(filePath);

        {
            if (request.Ev is TitchMusicEvent titch)
            {
                _logger.LogInformation("Ставим трек {data} от {name}", trackData, titch.TwitchUserName);
            }
            else
            {
                _logger.LogInformation("Ставим трек {data}", trackData);
            }
        }

        await _mpv.SetPlayerTrackAsync(filePath);

        // оказалось не нужно
        // if (piping?.Thumbnail != null)
        // {
        //     _logger.LogInformation("Докидываем видео");
        //     await _mpv.AddVideoAsync(piping.Thumbnail.TmpPath);
        // }

        int requestId = await WriteRequestToDbAsync(request, trackData, twitchUserDbId);

        lock (_lock)
        {
            if (request == _inProgress)
            {
                request.RequestId = requestId;
            }
        }

        if (piping == null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await piping.FfmpegTask.FinishTask;
            }
            catch
            {
                _piper.CancelPipingController(piping);
                return;
            }
            finally
            {
                piping.Yt.Process.Dispose();
                piping.FfmpegTask.Process.Dispose();
            }

            _logger.LogInformation("Мирно закрываем ффмпег таск {path}", piping.TmpFilePath);

            _piper.FinishPipingController(piping);
        });
    }

    private async Task<int> WriteRequestToDbAsync(RequestInProgress request, string trackData, int? twitchUserDbId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (request.Ev is TitchMusicEvent titch)
        {
            int? userId = await _database.GetTwitchUserIdAsync(titch.TwitchUserId);

            if (userId == null)
            {
                userId = await _database.AddTwitchUserAsync(titch.TwitchUserId, titch.TwitchUserName);
            }

            return await _database.AddTwitchRequestAsync(userId.Value, titch.Text, request.Ev.LinkInfo.Link, trackData,
                now);
        }

        if (request.Ev is ApiMusicEvent api)
        {
            if (api.BehalfTwitchUsername != null)
            {
                return await _database.AddApiRequestAsync(request.Ev.LinkInfo.Link, trackData, now,
                    twitchUserDbId.Value);
            }

            return await _database.AddApiRequestAsync(request.Ev.LinkInfo.Link, trackData, now);
        }

        throw new Exception($"че ты накодил епт {request.Ev.LinkInfo.GetType().Name}");
    }
}