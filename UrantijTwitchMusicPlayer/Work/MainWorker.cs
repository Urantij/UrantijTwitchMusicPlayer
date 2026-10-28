using Microsoft.Extensions.Options;
using TwitchSimpleLib.Chat.Messages;
using UrantijTwitchMusicPlayer.Data;
using UrantijTwitchMusicPlayer.Routes;
using UrantijTwitchMusicPlayer.Shared.Data.Models;
using UrantijTwitchMusicPlayer.Titch;
using UrantijTwitchMusicPlayer.Work.Ffmpeg;
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
    private readonly string _filterName = "myname";

    private readonly TitchClient _titch;
    private readonly MpvWorker _mpv;
    private readonly FfmpegWorker _ffmpeg;
    private readonly Piper _piper;
    private readonly CacheWorker _cache;
    private readonly Linker _linker;
    private readonly Database _database;

    private readonly MainConfig _config;

    private readonly ILogger _logger;

    private readonly Lock _lock = new();
    private RequestInProgress? _inProgress = null;

    public double? CurrentTrackLufs { get; private set; }

    public MainWorker(TitchClient titch, MpvWorker mpv, FfmpegWorker ffmpeg, Piper piper, CacheWorker cache,
        Linker linker,
        Database database, IOptions<MainConfig> mainOptions, ILogger<MainWorker> logger)
    {
        _titch = titch;
        _mpv = mpv;
        _ffmpeg = ffmpeg;
        _piper = piper;
        _cache = cache;
        _linker = linker;
        _database = database;
        _config = mainOptions.Value;
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
        _logger.LogDebug("раним ревард {link}", request.Ev.LinkInfo.Link);

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
        ILufsAnalyze? analyze = null;

        PipingController? piping = null;

        string genericLink = _linker.MakeGenericLink(request.Ev.LinkInfo);

        TrackCachedData? cacheData = await _database.FindTrackCacheAsync(genericLink);

        if (cacheData != null)
        {
            _logger.LogDebug("Нашли кешдату");

            // это не та модель но я не буду длетьа новую модель
            analyze = new LoudnormResult(cacheData.I, cacheData.TruePeak, cacheData.Lra, cacheData.Threshold);

            await _database.UpdateTrackCacheDateAsync(cacheData.Id, DateTimeOffset.UtcNow);
        }

        if (request.Ev.LinkInfo is YoutubeLinkInfo or SoundcloudLinkInfo)
        {
            filePath = _cache.Find(request.Ev.LinkInfo);

            if (filePath == null)
            {
                // TODO короч кансел тут может оставить временный файл не удалённым
                piping = await _piper.PipeAsync(request.Ev.LinkInfo.Link,
                    analyze != null ? LufzAnalyzeType.None : _config.MpvAnalyzeType, request.Cts.Token);
                await piping.FfmpegTask.StartingTask;

                while (!request.Cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), request.Cts.Token);

                    if (!File.Exists(piping.TmpFilePath))
                        continue;

                    FileInfo fileInfo = new(piping.TmpFilePath);

                    if (fileInfo.Length > _config.MpvMinFileSize || piping.FfmpegTask.FinishTask.IsCompleted)
                        break;
                }

                filePath = piping.TmpFilePath;
            }
            else if (analyze == null && _config.MpvAnalyzeType != LufzAnalyzeType.None)
            {
                // TODO здесь юзается не асинхронный вариант нормализации

                FfmpegParseResult parseResult =
                    await _ffmpeg.AnalyzeAsync(filePath, _config.MpvAnalyzeType, request.Cts.Token);

                analyze = parseResult.Analyze;

                cacheData = await _database.AddTrackCacheAsync(genericLink, parseResult.Analyze, DateTimeOffset.UtcNow);
            }
        }
        else if (request.Ev.LinkInfo is LocalFileLinkInfo)
        {
            filePath = request.Ev.LinkInfo.Link;

            if (analyze == null && _config.MpvAnalyzeType != LufzAnalyzeType.None)
            {
                FfmpegParseResult parseResult =
                    await _ffmpeg.AnalyzeAsync(filePath, _config.MpvAnalyzeType, request.Cts.Token);

                analyze = parseResult.Analyze;

                cacheData = await _database.AddTrackCacheAsync(genericLink, parseResult.Analyze, DateTimeOffset.UtcNow);
            }
        }
        else
        {
            // TODO убрать всё говно тут
            throw new Exception($"че ты накодил епт {request.Ev.LinkInfo.GetType().Name}");
        }

        string trackData = Path.GetFileNameWithoutExtension(filePath);

        // костылёк но хули делать епт)
        if (trackData.EndsWith(".tmp"))
        {
            trackData = trackData.Substring(0, trackData.Length - ".tmp".Length);
        }

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

        bool postFilter = false;
        if (piping?.FfmpegTask.AnalyzeTask != null)
        {
            if (piping.Yt.Dump.Filesize <= _config.MpvLargeFileThreshold)
            {
                _logger.LogDebug("Файл небольшой, подождём его тутачки");

                analyze = await piping.FfmpegTask.AnalyzeTask;

                cacheData = await _database.AddTrackCacheAsync(genericLink, analyze, DateTimeOffset.UtcNow);
            }
            else
            {
                _logger.LogDebug("ой блять файл сука здоровенный нахуй заебёт ждать");
                postFilter = true;
            }
        }

        string addArg;
        if (_config.MpvAnalyzeType == LufzAnalyzeType.None || analyze == null)
        {
            _logger.LogInformation("Один ступенька нормализация");

            addArg = "af=" + Loudnorm.LoudnormString(_filterName, _config.MpvTargetI, _config.MpvTargetTruePeak,
                _config.MpvTargetOnePasLra);
        }
        else
        {
            _logger.LogInformation("Два ступенька нормализация");

            addArg = "af=" + Loudnorm.LoudnormString(_filterName, _config.MpvTargetI, _config.MpvTargetTruePeak,
                _config.MpvTargetTwoPassLra, analyze.GetI(), analyze.GetTruePeak(), analyze.GetLra(),
                analyze.GetThreshold());
        }

        SongHandler song = await _mpv.SetPlayerTrackAsync(filePath, -1, addArg);

        Task? postTask = null;
        if (postFilter)
        {
            postTask = Task.Run(async () =>
            {
                analyze = await piping.FfmpegTask.AnalyzeTask;

                _logger.LogInformation("Два ступенька нормализация в постобработке");

                string updateFilter = Loudnorm.LoudnormString(_filterName, _config.MpvTargetI,
                    _config.MpvTargetTruePeak,
                    _config.MpvTargetTwoPassLra, analyze.GetI(), analyze.GetTruePeak(), analyze.GetLra(),
                    analyze.GetThreshold());

                await _mpv.UpdatePlayerFilterAsync(song, updateFilter);

                cacheData = await _database.AddTrackCacheAsync(genericLink, analyze, DateTimeOffset.UtcNow);
            });
        }

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
        {
            _logger.LogDebug("ревард маленький конец {link}", request.Ev.LinkInfo.Link);
            return;
        }

        _ = Task.Run(async () =>
        {
            _logger.LogDebug("ревард пост процесс {link}", request.Ev.LinkInfo.Link);

            try
            {
                await piping.FfmpegTask.FinishTask;

                // TODO его краши могут быть не связаны, нужно как то отдельно.
                if (postTask != null)
                    await postTask;
            }
            catch (TaskCanceledException)
            {
                _piper.CancelPipingController(piping);
                return;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "епты блин а че то ффмпег не смог завершить работу блин...");

                _piper.CancelPipingController(piping);
                return;
            }
            finally
            {
                piping.Yt.Process.Dispose();
                piping.FfmpegTask.Process.Dispose();
            }

            _logger.LogDebug("ревард пост процесс конец {link}", request.Ev.LinkInfo.Link);

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