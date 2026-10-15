using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Work;

public class CacheWorker
{
    private readonly string[] _cacheLocations;

    private readonly string[] _allowedExtensions;

    private readonly CaseInsensitiveValueComparer _comparer = new();

    public CacheWorker(IOptions<MainConfig> mainOptions)
    {
        _cacheLocations = mainOptions.Value.AudioSearchLocations;
        _allowedExtensions = mainOptions.Value.AudioExtensions;
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