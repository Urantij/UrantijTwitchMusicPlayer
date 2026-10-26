using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work.Ffmpeg;

public class FfmpegParseResult(string? artist, string? title, string? album, TimeSpan duration, double lufs)
{
    public string? Artist { get; } = artist;
    public string? Title { get; } = title;
    public string? Album { get; } = album;
    public TimeSpan Duration { get; } = duration;
    public double Lufs { get; } = lufs;
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

    public static readonly Regex NumberRegex = new(@" (?<number>-{0,1}\d+?\.\d+?) ", RegexOptions.Compiled);

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

            while (!cancellationToken.IsCancellationRequested)
            {
                string? errLine = await process.StandardError.ReadLineAsync(cancellationToken);

                if (errLine == null)
                {
                    startTcs.TrySetException(new Exception("не дождались линии"));
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
    public async Task<FfmpegParseResult> LufsAsync(string targetPath, CancellationToken cancellationToken = default)
    {
        using Process process = new();

        process.StartInfo.FileName = _ffmpegPath;
        process.StartInfo.ArgumentList.Add("-hide_banner");
        process.StartInfo.ArgumentList.Add("-nostats");
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(targetPath);
        process.StartInfo.ArgumentList.Add("-af");
        process.StartInfo.ArgumentList.Add("ebur128=framelog=verbose:dualmono=true:peak=true");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("null");
        process.StartInfo.ArgumentList.Add("-");

        process.StartInfo.RedirectStandardError = true;

        CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() => { process.Close(); });

        process.Start();

        //       Metadata:
        // ALBUM           : サクラノ詩 ヴォーカルCD
        // ARTIST          : monet
        // comment         : 163 key(Don't modify):cEgtqHxwpdRKcBCRPBlPktlHhmrTcizixpMvRszN1/t01RXkRMIbbZ03dHP6iSpQTkeB63cM6S9ZirsgIbKnZ7HrNFhd72MteheD3GIdx+RBVrHi2P55iGQ06ubJ5Nr0GJxxkZRPMWpJJqc3DgknyO2lc33CMmPaP1oZxYRA1KxPGsteR6cTudVevWrWb1d+CnOXlOg9njuDl9zYyht7bAroJ2UEzcUQp+zKTxsze
        // ENCODER         : Lavf58.45.100
        // TITLE           : ZYPRESSENの花束
        // track           : 4

        // Duration: 00:04:23.37, start: 0.000000, bitrate: 5338 kb/s

        string? artist = null; // TODO сделать бы, но впадлу
        string? title = null;
        string? album = null;
        TimeSpan? duration = null;
        double? lufs = null;

        Task errorTask = Task.Run(async () =>
        {
            bool ending = false;

            while (true)
            {
                string? readLine = await process.StandardError.ReadLineAsync(cancellationToken);

                if (readLine == null)
                    return;

                if (ending)
                {
                    if (lufs == null)
                    {
                        var match = Reggexx.Match(readLine);

                        if (match.Success)
                        {
                            lufs = double.Parse(match.Groups["lufs"].Value);
                        }
                    }
                }
                else if (readLine.StartsWith("[Parsed_ebur128_0 @"))
                {
                    ending = true;
                }
                else
                {
                    if (duration == null)
                    {
                        var audioDataRegex = AudioDataRegex.Match(readLine);

                        if (audioDataRegex.Success)
                        {
                            duration = TimeSpan.Parse(audioDataRegex.Groups["duration"].Value);
                            continue;
                        }
                    }
                }
            }
        });

        //[Parsed_ebur128_0 @ 0x7f9ad80029c0] Summary:

        //   Integrated loudness:
        //     I:          -8.3 LUFS
        //     Threshold: -18.4 LUFS

        //   Loudness range:
        //     LRA:         6.7 LU
        //     Threshold: -28.4 LUFS
        //     LRA low:   -11.9 LUFS
        //     LRA high:   -5.3 LUFS

        //   True peak:
        //     Peak:        1.3 dBFS

        await errorTask;

        cancellationRegistration.Unregister();

        cancellationToken.ThrowIfCancellationRequested();

        if (lufs == null || duration == null)
            throw new Exception(); // TODO

        FfmpegParseResult result = new(artist, title, album, duration.Value, lufs.Value);

        return result;
    }
}