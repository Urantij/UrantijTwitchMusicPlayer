using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work.Yt;

[JsonSerializable(typeof(YtDlpDump))]
internal partial class YtDlpJsonSerializationContext : JsonSerializerContext;

public class YtDlpController(Process process, YtDlpDump dump) : IDisposable
{
    public Process Process { get; } = process;
    public YtDlpDump Dump { get; } = dump;

    public void Dispose()
    {
        Process.Dispose();
    }
}

public class YtDlpWorker
{
    private readonly ILogger _logger;
    private readonly string _location;
    private readonly string? _proxyUrl;

    // ERROR: [youtube] dyqbYEQ_a6g: Video unavailable
    private readonly Regex _errorRegex = new(@"^ERROR: \[youtube\] (.+?): (?<text>.+)$");

    public YtDlpWorker(IOptions<MainConfig> mainOptions, ILogger<YtDlpWorker> logger)
    {
        _logger = logger;
        _location = mainOptions.Value.AudioCacheLocation;
        _proxyUrl = mainOptions.Value.YoutubeProxyUrl;
    }

    public async Task<YtDlpController> StreamDownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        Process process = new();
        process.StartInfo.ArgumentList.Add("--cookies-from-browser");
        process.StartInfo.ArgumentList.Add("firefox");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("bestaudio/best");
        if (_proxyUrl != null)
        {
            process.StartInfo.ArgumentList.Add("--proxy");
            process.StartInfo.ArgumentList.Add(_proxyUrl);
        }

        process.StartInfo.ArgumentList.Add("--dump-json");
        process.StartInfo.ArgumentList.Add("--no-simulate");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("-");
        process.StartInfo.ArgumentList.Add(url);
        process.StartInfo.FileName = "yt-dlp";

        try
        {
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            Task<YtDlpDump> errorTask = Task.Run<YtDlpDump>(async () =>
            {
                string? errorData = await process.StandardError.ReadLineAsync(cancellationToken);

                if (errorData == null)
                    throw new Exception("Чето не пришла строка епт...");

                // ERROR: [youtube] dyqbYEQ_a6g: Video unavailable
                // TODO обрабатывать ошибки

                Match errorMatch = _errorRegex.Match(errorData);

                if (errorMatch.Success)
                {
                    string text = errorMatch.Groups["text"].Value;

                    throw new Exception($"Ошибка внутри yt-dlp {text}");
                }

                try
                {
                    return (YtDlpDump)JsonSerializer.Deserialize(errorData, typeof(YtDlpDump),
                        YtDlpJsonSerializationContext.Default);
                }
                catch (Exception e)
                {
                    if (errorData.Length > 1000)
                    {
                        _logger.LogError(e, "Десериализация провалилась. {count} символов", errorData.Length);
                    }
                    else
                    {
                        _logger.LogError(e, "Десериализация провалилась. {content}", errorData);
                    }

                    throw;
                }
            });

            YtDlpDump dump = await errorTask;

            cancellationToken.ThrowIfCancellationRequested();

            return new YtDlpController(process, dump);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    public async Task<string> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        // yt-dlp --cookies-from-browser firefox -f bestaudio --embed-metadata --write-thumbnail --proxy socks5://localhost:10808 --print-to-file after_move:filepath ./data.txt https://www.youtube.com/watch\?v=A9H--94-gxE

        using Process process = new();

        string dataFilePath = Path.GetTempFileName();

        process.StartInfo.ArgumentList.Add("--cookies-from-browser");
        process.StartInfo.ArgumentList.Add("firefox");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("bestaudio");
        process.StartInfo.ArgumentList.Add("--embed-metadata");
        process.StartInfo.ArgumentList.Add("--write-thumbnail");
        process.StartInfo.ArgumentList.Add("--proxy");
        process.StartInfo.ArgumentList.Add("socks5://localhost:10808");
        process.StartInfo.ArgumentList.Add("-P");
        process.StartInfo.ArgumentList.Add(_location);
        process.StartInfo.ArgumentList.Add("--print-to-file");
        process.StartInfo.ArgumentList.Add("after_move:filepath");
        process.StartInfo.ArgumentList.Add(dataFilePath);
        process.StartInfo.ArgumentList.Add(url);
        process.StartInfo.FileName = "yt-dlp";

        CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() => { process.Close(); });

        process.Start();

        await process.WaitForExitAsync(
            cancellationToken); // здесь канцелятион токен не нужен, он может тока изменить флоу не в ту сторону

        cancellationRegistration.Unregister();

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new Exception();

        string audioFilePath = (await File.ReadAllTextAsync(dataFilePath, cancellationToken)).TrimEnd('\n');
        File.Delete(dataFilePath);

        if (!File.Exists(audioFilePath))
        {
            throw new Exception("Не нашли по итогу аудиофайл, втф");
        }

        return audioFilePath;
    }
}