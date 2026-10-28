using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work.Ffmpeg;

public class FfmpegParseResult(string? artist, string? title, string? album, TimeSpan duration, ILufsAnalyze analyze)
{
    public string? Artist { get; } = artist;
    public string? Title { get; } = title;
    public string? Album { get; } = album;
    public TimeSpan Duration { get; } = duration;
    public ILufsAnalyze Analyze { get; } = analyze;
}

public class FfmpegPipingController(
    Process process,
    Task startingTask,
    Task<ILufsAnalyze>? analyzeTask,
    Task finishTask)
{
    public Process Process { get; } = process;
    public Task StartingTask { get; } = startingTask;
    public Task<ILufsAnalyze>? AnalyzeTask { get; } = analyzeTask;
    public Task FinishTask { get; } = finishTask;
}

public enum LufzAnalyzeType
{
    None,
    Ebur,
    Loudnorm
}

public class FfmpegWorker
{
    private readonly ILogger<FfmpegWorker> _logger;

    // ffmpeg -hide_banner -nostats -i &quot;$audio&quot; -af ebur128=framelog=verbose:dualmono=true:peak=true -f null - 2>&1

    // TODO атрибут
    private readonly Regex Reggexx = new("I:\\s*(?<lufs>.+) LUFS", RegexOptions.Compiled);

    // Duration: 00:04:23.37, start: 0.000000, bitrate: 5338 kb/s
    private readonly Regex AudioDataRegex =
        new(@"Duration: (?<duration>[\d\.:]+), start: (.+?) bitrate: ", RegexOptions.Compiled);

    private readonly Regex FfmpegStreamLine =
        // new(@"Stream #0:0\((.+?)\): Audio: (.+?), (.+?) Hz, stereo, fltp (default)", RegexOptions.Compiled);
        new(@"Stream #0:0\((.+?)\): Audio: (.+?), (.+?) Hz", RegexOptions.Compiled);

    private readonly string _ffmpegPath;
    private readonly bool _displayStats;

    public FfmpegWorker(IOptions<MainConfig> mainOptions, ILogger<FfmpegWorker> logger)
    {
        _logger = logger;
        _ffmpegPath = mainOptions.Value.FfmpegPath;
        _displayStats = mainOptions.Value.DisplayFfmpegStats;
    }

    public FfmpegPipingController PipeToFile(Stream stream, string filePath,
        List<(string key, string value)> meta, LufzAnalyzeType lufzAnalyzeType,
        CancellationToken cancellationToken = default)
    {
        Process process = new();

        process.StartInfo.FileName = _ffmpegPath;
        process.StartInfo.ArgumentList.Add("-hide_banner");
        if (_displayStats)
            process.StartInfo.ArgumentList.Add("-nostats");
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add("pipe:0");
        process.StartInfo.ArgumentList.Add("-c:a");
        process.StartInfo.ArgumentList.Add("copy");
        foreach (var pair in meta)
        {
            process.StartInfo.ArgumentList.Add("-metadata");
            process.StartInfo.ArgumentList.Add($"{pair.key}={pair.value}");
        }

        process.StartInfo.ArgumentList.Add(filePath);

        TaskCompletionSource<ILufsAnalyze>? analyzeTcs = null;
        if (lufzAnalyzeType == LufzAnalyzeType.Ebur)
        {
            process.StartInfo.ArgumentList.Add("-filter_complex");
            process.StartInfo.ArgumentList.Add("[0:a]ebur128=framelog=verbose:dualmono=true:peak=true[out_analyze]");
            process.StartInfo.ArgumentList.Add("-map");
            process.StartInfo.ArgumentList.Add("[out_analyze]");
            process.StartInfo.ArgumentList.Add("-f");
            process.StartInfo.ArgumentList.Add("null");
            process.StartInfo.ArgumentList.Add("-");

            analyzeTcs = new TaskCompletionSource<ILufsAnalyze>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        else if (lufzAnalyzeType == LufzAnalyzeType.Loudnorm)
        {
            process.StartInfo.ArgumentList.Add("-filter_complex");
            process.StartInfo.ArgumentList.Add("[0:a]loudnorm=print_format=json[out_analyze]");
            process.StartInfo.ArgumentList.Add("-map");
            process.StartInfo.ArgumentList.Add("[out_analyze]");
            process.StartInfo.ArgumentList.Add("-f");
            process.StartInfo.ArgumentList.Add("null");
            process.StartInfo.ArgumentList.Add("-");

            analyzeTcs = new TaskCompletionSource<ILufsAnalyze>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        else if (lufzAnalyzeType != LufzAnalyzeType.None)
        {
            _logger.LogWarning("Пришёл неизвестный типок {type}", lufzAnalyzeType);
        }

        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardError = true;

        process.Start();

        TaskCompletionSource startTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // TODO я не знаю нужен ли таск
        Task inTask = Task.Run(async () =>
        {
            await stream.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            await process.StandardInput.FlushAsync(cancellationToken);
            process.StandardInput.Close();

            _logger.LogInformation("{path} стрим аутпут закончился", filePath);
        }, cancellationToken);

        Task errTask = Task.Run(async () =>
        {
            bool seenTheLight = false;
            ILufsAnalyze? lufsAnalyze = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? errLine = await process.StandardError.ReadLineAsync(cancellationToken);

                    if (errLine == null)
                    {
                        startTcs.TrySetException(new Exception("не дождались линии"));
                        analyzeTcs?.TrySetException(new Exception("не дождались линии"));
                        return;
                    }

                    if (seenTheLight)
                    {
                        if (lufsAnalyze != null)
                            continue;

                        if (lufzAnalyzeType == LufzAnalyzeType.Ebur)
                        {
                            if (Ebur128.CheckIfStart(errLine))
                            {
                                lufsAnalyze =
                                    await Ebur128.ReadErrStreamAsync(process.StandardError, cancellationToken);

                                analyzeTcs?.SetResult(lufsAnalyze);
                            }
                        }
                        else if (lufzAnalyzeType == LufzAnalyzeType.Loudnorm)
                        {
                            if (Loudnorm.CheckIfStart(errLine))
                            {
                                lufsAnalyze =
                                    await Loudnorm.ReadErrStreamAsync(process.StandardError, cancellationToken);

                                analyzeTcs?.SetResult(lufsAnalyze);
                            }
                        }

                        continue;
                    }

                    if (!FfmpegStreamLine.IsMatch(errLine))
                        continue;

                    _logger.LogDebug("Нашли свет");

                    seenTheLight = true;

                    startTcs.SetResult();
                }
            }
            catch (Exception e)
            {
                startTcs.TrySetException(e);
                analyzeTcs?.TrySetException(e);
            }
            finally
            {
                startTcs.TrySetCanceled();
                analyzeTcs?.TrySetCanceled();
            }
        }, cancellationToken);

        Task finishTask = Task.Run(async () =>
        {
            await process.WaitForExitAsync(cancellationToken);
            await inTask;
            await errTask;

            if (process.ExitCode != 0)
                throw new Exception($"екзит код {process.ExitCode}");
        }, cancellationToken);

        return new FfmpegPipingController(process, startTcs.Task, analyzeTcs?.Task, finishTask);
    }

    // TODO канцелятион
    public async Task<FfmpegParseResult> AnalyzeAsync(string targetPath, LufzAnalyzeType lufzAnalyzeType,
        CancellationToken cancellationToken = default)
    {
        using Process process = new();

        process.StartInfo.FileName = _ffmpegPath;
        process.StartInfo.ArgumentList.Add("-hide_banner");
        process.StartInfo.ArgumentList.Add("-nostats");
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(targetPath);
        process.StartInfo.ArgumentList.Add("-af");
        if (lufzAnalyzeType == LufzAnalyzeType.Ebur)
        {
            process.StartInfo.ArgumentList.Add("ebur128=framelog=verbose:dualmono=true:peak=true");
        }
        else if (lufzAnalyzeType == LufzAnalyzeType.Loudnorm)
        {
            process.StartInfo.ArgumentList.Add("loudnorm=print_format=json");
        }
        else
        {
            throw new Exception($"че это за чучело в луфсах? {lufzAnalyzeType}");
        }

        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("null");
        process.StartInfo.ArgumentList.Add("-");

        process.StartInfo.RedirectStandardError = true;

        CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() => { process.Close(); });

        process.Start();

        string? artist = null; // TODO сделать бы, но впадлу
        string? title = null;
        string? album = null;
        TimeSpan? duration = null;

        Task<ILufsAnalyze> errorTask = Task.Run<ILufsAnalyze>(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? readLine = await process.StandardError.ReadLineAsync(cancellationToken);

                if (readLine == null)
                    throw new Exception("readline null");

                if (lufzAnalyzeType == LufzAnalyzeType.Ebur && Ebur128.CheckIfStart(readLine))
                {
                    return await Ebur128.ReadErrStreamAsync(process.StandardError, cancellationToken);
                }

                if (lufzAnalyzeType == LufzAnalyzeType.Loudnorm && Loudnorm.CheckIfStart(readLine))
                {
                    return await Loudnorm.ReadErrStreamAsync(process.StandardError, cancellationToken);
                }

                if (duration == null)
                {
                    var audioDataRegex = AudioDataRegex.Match(readLine);

                    if (audioDataRegex.Success)
                    {
                        duration = TimeSpan.Parse(audioDataRegex.Groups["duration"].Value);
                    }
                }
            }

            throw new Exception("конец лол");
        }, cancellationToken);

        ILufsAnalyze analyze = await errorTask;

        cancellationRegistration.Unregister();

        cancellationToken.ThrowIfCancellationRequested();

        if (duration == null)
            throw new Exception(); // TODO

        FfmpegParseResult result = new(artist, title, album, duration.Value, analyze);

        return result;
    }
}