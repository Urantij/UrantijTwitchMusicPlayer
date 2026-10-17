using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work;

public class FfmpegParseResult(string? artist, string? title, string? album, TimeSpan duration, double lufs)
{
    public string? Artist { get; } = artist;
    public string? Title { get; } = title;
    public string? Album { get; } = album;
    public TimeSpan Duration { get; } = duration;
    public double Lufs { get; } = lufs;
}

public class FfmpegWorker
{
    // ffmpeg -hide_banner -nostats -i &quot;$audio&quot; -af ebur128=framelog=verbose:dualmono=true:peak=true -f null - 2>&1

    // TODO атрибут
    private readonly Regex Reggexx = new("I:\\s*(?<lufs>.+) LUFS", RegexOptions.Compiled);

    // Duration: 00:04:23.37, start: 0.000000, bitrate: 5338 kb/s
    private readonly Regex AudioDataRegex =
        new(@"Duration: (?<duration>[\d\.:]+), start: (.+?) bitrate: ", RegexOptions.Compiled);

    private readonly string _ffmpegPath;

    public FfmpegWorker(IOptions<MainConfig> mainOptions)
    {
        _ffmpegPath = mainOptions.Value.FfmpegPath;
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