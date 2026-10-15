using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace UrantijTwitchMusicPlayer.Work;

public class CacheWorker
{
    private readonly string[] _cacheLocations =
    [
        "/home/punky/Music/karina/stream/",
        "/home/punky/Music/karina"
    ];

    private readonly string[] _allowedExtensions =
    [
        ".webm",
        ".mp4",
        ".mp3"
    ];

    private readonly CaseInsensitiveValueComparer _comparer = new();

    public CacheWorker()
    {
    }

    public string? Find(LinkInfo linkInfo)
    {
        if (linkInfo is YoutubeLinkInfo youtube)
        {
            string includedString = $"[{youtube.Id}]";

            foreach (string location in _cacheLocations)
            {
                foreach (string filePath in Directory.GetFiles(location))
                {
                    // Вольный Путь - То ли байка, то ли ложь [ILSR88ETePo].webm

                    string extension = Path.GetExtension(filePath);

                    if (!_allowedExtensions.Contains(extension, _comparer))
                        continue;

                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    if (fileName.EndsWith(includedString))
                        return filePath;
                }
            }
        }

        return null;
    }
}