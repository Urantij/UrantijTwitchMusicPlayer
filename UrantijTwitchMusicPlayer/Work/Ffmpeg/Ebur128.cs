using System.Text.RegularExpressions;

namespace UrantijTwitchMusicPlayer.Work.Ffmpeg;

public class Ebur128Result(
    Ebur128Result.IL integratedLoudness,
    Ebur128Result.LR loudnessRange,
    Ebur128Result.TP truePeak) : ILufsAnalyze
{
    public struct IL
    {
        public double I;
        public double Threshold;
    }

    public struct LR
    {
        public double LRA;
        public double Threshold;
        public double LRA_low;
        public double LRA_high;
    }

    public struct TP
    {
        public double Peak;
    }

    public IL IntegratedLoudness { get; } = integratedLoudness;
    public LR LoudnessRange { get; } = loudnessRange;
    public TP TruePeak { get; } = truePeak;

    public double GetI() => IntegratedLoudness.I;
    public double GetThreshold() => IntegratedLoudness.Threshold;
    public double GetLra() => LoudnessRange.LRA;
    public double GetTruePeak() => TruePeak.Peak;
}

public class Ebur128
{
    public static bool CheckIfStart(string line)
    {
        // [Parsed_ebur128_0 @ 0x7f78800029c0] Summary:59.5x elapsed=0:00:02.50    

        return line.StartsWith("[Parsed_ebur128_0 @");
    }

    public static async Task<Ebur128Result> ReadErrStreamAsync(StreamReader errReader,
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

        // [Parsed_ebur128_0 @ 0x7f78800029c0] Summary:59.5x elapsed=0:00:02.50    
        //
        // Integrated loudness:
        // I:         -12.0 LUFS
        // Threshold: -22.3 LUFS
        //
        //     Loudness range:
        // LRA:         8.9 LU
        // Threshold: -32.2 LUFS
        //     LRA low:   -19.4 LUFS
        //     LRA high:  -10.6 LUFS
        //
        //     True peak:
        // Peak:        1.0 dBFS

        await errReader.ReadLineAsync(cancellationToken);
        await errReader.ReadLineAsync(cancellationToken);

        double i = await ParseLineAsync();
        double thr = await ParseLineAsync();

        await errReader.ReadLineAsync(cancellationToken);
        await errReader.ReadLineAsync(cancellationToken);

        double lra = await ParseLineAsync();
        double lrThr = await ParseLineAsync();
        double lraLow = await ParseLineAsync();
        double lraHigh = await ParseLineAsync();

        await errReader.ReadLineAsync(cancellationToken);
        await errReader.ReadLineAsync(cancellationToken);

        double peak = await ParseLineAsync();

        return new Ebur128Result(new Ebur128Result.IL
            {
                I = i,
                Threshold = thr
            },
            new Ebur128Result.LR
            {
                LRA = lra,
                Threshold = lrThr,
                LRA_low = lraLow,
                LRA_high = lraHigh
            },
            new Ebur128Result.TP
            {
                Peak = peak
            });
    }
}