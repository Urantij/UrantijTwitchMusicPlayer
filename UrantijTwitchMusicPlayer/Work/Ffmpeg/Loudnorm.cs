using System.Text.RegularExpressions;

namespace UrantijTwitchMusicPlayer.Work.Ffmpeg;

public class LoudnormResult(double inputI, double inputTp, double inputLra, double inputThresh) : ILufsAnalyze
{
    public double InputI { get; } = inputI;
    public double InputTP { get; } = inputTp;
    public double InputLRA { get; } = inputLra;
    public double InputThresh { get; } = inputThresh;

    public double GetI() => InputI;
    public double GetThreshold() => InputThresh;
    public double GetLra() => InputLRA;
    public double GetTruePeak() => InputTP;
}

public class Loudnorm
{
    public static bool CheckIfStart(string line)
    {
        // [Parsed_loudnorm_0 @ 0x7f53600029c0]   

        return line.StartsWith("[Parsed_loudnorm_0");
    }

    public static async Task<LoudnormResult> ReadErrStreamAsync(StreamReader errReader,
        CancellationToken cancellationToken)
    {
        async Task<double> ParseLineAsync()
        {
            string? line = await errReader.ReadLineAsync(cancellationToken);

            if (line == null)
                throw new Exception("I: null");

            Match m = FfmpegWorker.NumberRegex.Match(line);

            if (!m.Success)
                throw new Exception($"не парсится блин {line}");

            return double.Parse(m.Groups["number"].Value);
        }

        // [Parsed_loudnorm_0 @ 0x7f53600029c0] 
        // {
        //     "input_i" : "-12.05",
        //     "input_tp" : "1.00",
        //     "input_lra" : "9.20",
        //     "input_thresh" : "-22.39",
        //     "output_i" : "-22.57",
        //     "output_tp" : "-2.00",
        //     "output_lra" : "4.00",
        //     "output_thresh" : "-32.71",
        //     "normalization_type" : "dynamic",
        //     "target_offset" : "-1.43"
        // }

        await errReader.ReadLineAsync(cancellationToken);

        double i = await ParseLineAsync();
        double tp = await ParseLineAsync();
        double lra = await ParseLineAsync();
        double thresh = await ParseLineAsync();


        return new LoudnormResult(i, tp, lra, thresh);
    }
}