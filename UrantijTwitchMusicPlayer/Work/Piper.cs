using System.Net;
using Microsoft.Extensions.Options;
using UrantijTwitchMusicPlayer.Work.Ffmpeg;
using UrantijTwitchMusicPlayer.Work.Yt;

namespace UrantijTwitchMusicPlayer.Work;

public class PipingController(
    YtDlpController yt,
    string tmpFilePath,
    PipingController.ThumbnailM? thumbnail,
    FfmpegPipingController ffmpegTask)
{
    public class ThumbnailM(string ext, string tmpPath)
    {
        public string Ext { get; } = ext;
        public string TmpPath { get; } = tmpPath;
    }

    public YtDlpController Yt { get; } = yt;
    public string TmpFilePath { get; } = tmpFilePath;
    public ThumbnailM? Thumbnail { get; } = thumbnail;
    public FfmpegPipingController FfmpegTask { get; } = ffmpegTask;
}

public class Piper : IDisposable
{
    private readonly YtDlpWorker _yt;
    private readonly FfmpegWorker _ffmpeg;
    private readonly HttpClient _http;
    private readonly string _location;
    private readonly bool _downloadThumbnail;
    private readonly long _crashFileSize;
    private readonly ILogger _logger;

    public Piper(YtDlpWorker yt, FfmpegWorker ffmpeg, IOptions<MainConfig> mainOptions,
        ILogger<Piper> logger)
    {
        _yt = yt;
        _ffmpeg = ffmpeg;

        SocketsHttpHandler handler = new();

        if (mainOptions.Value.YoutubeProxyUrl is not null)
        {
            handler.Proxy = new WebProxy(mainOptions.Value.YoutubeProxyUrl);
        }

        _http = new HttpClient(handler, true);
        _location = mainOptions.Value.AudioCacheLocation;
        _downloadThumbnail = mainOptions.Value.DownloadThumbnail;
        _crashFileSize = mainOptions.Value.DownloadFileMaxPermittedSize;
        _logger = logger;
    }

    public async Task<PipingController> PipeAsync(string url, LufzAnalyzeType lufzAnalyzeType,
        CancellationToken cancellationToken = default)
    {
        YtDlpController controller = await _yt.StreamDownloadAsync(url, cancellationToken);

        if (controller.Process.HasExited)
        {
            // тута ошибка случилась ептыть)))
            throw new Exception("Ютуп закрылся раньше нужного))");
        }

        if (controller.Dump.Filesize > _crashFileSize)
            throw new Exception($"блять там файл пиздец здоровый ты че ебанутый?? {controller.Dump.Filesize}");

        string tmpFileName = GenerateFileName(controller.Dump, true);
        string tmpFilePath = Path.Combine(_location, tmpFileName);

        PipingController.ThumbnailM? thumbnail = null;

        List<(string key, string value)> metadata = MakeMetadata(controller.Dump);

        _logger.LogInformation("Пишем {loc}", tmpFilePath);

        FfmpegPipingController ffmpegTask;

        // TODO не оч элегантно но хули делать епт
        try
        {
            ffmpegTask = _ffmpeg.PipeToFile(controller.Process.StandardOutput.BaseStream,
                tmpFilePath, metadata, lufzAnalyzeType, cancellationToken);
        }
        catch
        {
            if (File.Exists(tmpFilePath))
                File.Delete(tmpFilePath);

            throw;
        }

        if (!_downloadThumbnail)
            return new PipingController(controller, tmpFilePath, thumbnail, ffmpegTask);

        string ext = controller.Dump.Thumbnail.Split('.').Last();
        string imgFileName = GenerateFileName(controller.Dump, true, extensionOverride: ext);
        string imgPath = Path.Combine(_location, imgFileName);

        try
        {
            _logger.LogInformation("Качаем тамбнейл {path}", imgPath);

            await DownloadThumbnailAsync(controller.Dump, imgPath, cancellationToken);

            _logger.LogInformation("Скачали!");

            thumbnail = new PipingController.ThumbnailM(ext, imgPath);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(imgPath))
                File.Delete(imgPath);

            throw;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Не удалось скачать тамбнейл");
        }

        return new PipingController(controller, tmpFilePath, thumbnail, ffmpegTask);
    }

    public void CancelPipingController(PipingController controller)
    {
        _logger.LogInformation("Удаляем недокаченный файл {loc}", controller.TmpFilePath);

        File.Delete(controller.TmpFilePath);
        if (controller.Thumbnail != null)
            File.Delete(controller.Thumbnail.TmpPath);
    }

    public void FinishPipingController(PipingController controller)
    {
        _logger.LogInformation("Двигаем файл {loc}", controller.TmpFilePath);

        {
            string fileName = GenerateFileName(controller.Yt.Dump, false);
            string filePath = Path.Combine(_location, fileName);

            File.Move(controller.TmpFilePath, filePath);
        }

        if (controller.Thumbnail != null)
        {
            string fileName = GenerateFileName(controller.Yt.Dump, false, extensionOverride: controller.Thumbnail.Ext);
            string filePath = Path.Combine(_location, fileName);

            File.Move(controller.Thumbnail.TmpPath, filePath);
        }
    }

    public async Task DownloadThumbnailAsync(YtDlpDump dump, string path, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.GetAsync(dump.Thumbnail, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using FileStream fs = new(path, FileMode.Create);

        await response.Content.CopyToAsync(fs, cancellationToken);
    }

    public static List<(string key, string value)> MakeMetadata(YtDlpDump dump)
    {
        List<(string key, string value)> result = new();

        // с музыкой
        // title           : Рейв
        // artist          : BY BABA YAGA
        // album           : Рейв
        // date            : 20260827
        // comment         : https://www.youtube.com/watch?v=5IVPwHbj7x0
        // description     : Provided to YouTube by BABA YAGA BY

        // TODO в файле ордер метаданных странный
        if (dump.Track != null && dump.Artist != null)
        {
            result.Add(("title", dump.Track));
            result.Add(("artist", dump.Artist));
            if (dump.Album != null)
                result.Add(("album", dump.Album));
            result.Add(("date", dump.ReleaseDate ?? dump.UploadDate));
        }
        else
        {
            result.Add(("title", dump.Title));
            result.Add(("artist", dump.Channel));
            result.Add(("date", dump.UploadDate));
        }

        result.Add(("comment", dump.OriginalUrl));
        result.Add(("description", dump.Description));

        // без музыки
        // title           : Павел Пламенев - Быстро сгорая (альбомная версия "Герой с тысячью лиц" 2016 г.).
        // ARTIST          : Plamenev
        // GENRE           : People & Blogs
        // DATE            : 20170622
        // DESCRIPTION     : Билеты на все концерты Пламенева: https://plamenev.ru/koncert/

        return result;
    }

    public static string GenerateFileName(YtDlpDump dump, bool tmp, string? extensionOverride = null)
    {
        string ext = extensionOverride ?? dump.Ext;

        if (tmp)
        {
            ext = "tmp." + ext;
        }

        if (dump.Artist != null && dump.Track != null)
        {
            return $"{dump.Artist} - {dump.Track} [{dump.Id}].{ext}";
        }

        return $"{dump.Title} [{dump.Id}].{ext}";
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}