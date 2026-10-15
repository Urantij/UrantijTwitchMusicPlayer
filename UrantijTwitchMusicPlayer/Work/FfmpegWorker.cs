using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work;

public class FfmpegWorker
{
    // ffmpeg -hide_banner -nostats -i &quot;$audio&quot; -af ebur128=framelog=verbose:dualmono=true:peak=true -f null - 2>&1

    // TODO атрибут
    private readonly Regex Reggexx = new("I:\\s*(?<lufs>.+) LUFS", RegexOptions.Compiled);

    private readonly string _ffmpegPath;

    public FfmpegWorker(IOptions<MainConfig> mainOptions)
    {
        _ffmpegPath = mainOptions.Value.FfmpegPath;
    }

    // TODO канцелятион
    public async Task<double> LufsAsync(string targetPath, CancellationToken cancellationToken = default)
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

        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

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


        string text = await errorTask;

        cancellationRegistration.Unregister();

        cancellationToken.ThrowIfCancellationRequested();

        int start = text.IndexOf("Integrated loudness:", StringComparison.Ordinal);

        string targetText = text.Substring(start);

        var match = Reggexx.Match(targetText);

        if (!match.Success)
        {
            // TODO
            throw new Exception();
        }

        return double.Parse(match.Groups["lufs"].Value);
    }
}