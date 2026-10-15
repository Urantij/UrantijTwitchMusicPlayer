using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work;

public class YtDlpWorker
{
    private readonly string _location;

    public YtDlpWorker(IOptions<MainConfig> mainOptions)
    {
        _location = mainOptions.Value.AudioCacheLocation;
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