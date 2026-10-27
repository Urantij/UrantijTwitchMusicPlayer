using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using UrantijTwitchMusicPlayer.Player;

namespace UrantijTwitchMusicPlayer.Work;

public class SongHandler(string filePath)
{
    public string FilePath { get; } = filePath;
}

public class MpvWorker : IHostedService
{
    private MpvPlayerHandler? _player;

    private readonly SemaphoreSlim _locker = new(0, 1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MpvWorker> _logger;

    private readonly bool _autoStart;
    private readonly string _cachePath;

    // по моим прикидкам, -8.6 луфс трек нормально звучит на 52%
    // это вайбкод, я не умею думать и писать формулы
    private const double DefaultTargetVolumePercent = 56.0; // 52.0;
    private const double DefaultTargetLufs = -7.4; // -8.6;

    // в мпв 60% громкости это -9.4дб (я не до конца понимаю)
    private double _targetVolumePercent;
    private double _targetLufs;
    private double _baseVolumeDb;

    private SongHandler? _song;

    public MpvWorker(IServiceProvider serviceProvider, IOptions<MainConfig> mainOptions, ILogger<MpvWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _autoStart = mainOptions.Value.AutoStartMpv;
        _cachePath = mainOptions.Value.MpvSavePath;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_cachePath))
        {
            IAsyncEnumerator<string> a = File.ReadLinesAsync(_cachePath, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            await a.MoveNextAsync();
            double volume = double.Parse(a.Current, CultureInfo.InvariantCulture);
            await a.MoveNextAsync();
            double lufs = double.Parse(a.Current, CultureInfo.InvariantCulture);

            SetSoundData(volume, lufs);
        }
        else
        {
            SetSoundData(DefaultTargetVolumePercent, DefaultTargetLufs);
        }

        _logger.LogInformation("{volume}% {lufs} lufs", _targetVolumePercent, _targetLufs);

        if (_autoStart)
        {
            await CreateWindowAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task UpdateSoundDataAsync(double volume, double lufs)
    {
        _logger.LogInformation("Ставим новые значения {volume}% {lufs} lufs", volume, lufs);

        SetSoundData(volume, lufs);

        await File.WriteAllLinesAsync(_cachePath, [
            volume.ToString(CultureInfo.InvariantCulture),
            lufs.ToString(CultureInfo.InvariantCulture)
        ]);
    }

    private void SetSoundData(double volume, double lufs)
    {
        _targetVolumePercent = volume;
        _targetLufs = lufs;
        _baseVolumeDb = 60 * (Math.Pow(_targetVolumePercent / 100.0, 1.0 / 3.0) - 1.0);
    }

    // вайбкод))
    public double CalculateAoVolumeDb(double trackLufs, double truePeak)
    {
        // 1. Вычисляем желаемое усиление
        double gain = _targetLufs - trackLufs;

        // 2. Считаем лимит безопасности на основе True Peak
        // Запас в -0.1 или -0.2 дБ оставляем, чтобы точно не клипповало
        double maxSafeGain = -0.1 - truePeak;

        // 3. Если мы хотим сделать громче (gain > 0), проверяем, безопасно ли это
        if (gain > 0 && gain > maxSafeGain)
        {
            // Если лимит безопасности позволяет хоть немного усилить — усиливаем до лимита.
            // Если лимит отрицательный (уже клиппует), то gain станет меньше нуля (приглушит трек).
            gain = maxSafeGain;
        }

        // Жесткий лимит снизу/сверху на всякий случай
        if (gain < -30.0) gain = -30.0;
        if (gain > 12.0) gain = 12.0;

        return Math.Round(gain, 2);
    }

    public double CalculateVolumePercentForLufs(double trackLufs)
    {
        // 1. Находим чистую разницу в дБ. 
        // Если трек тише (например -15), то -8.6 - (-15) = +6.4 дБ (нужно сделать громче)
        double gain = _targetLufs - trackLufs;

        // 2. Просто прибавляем эту разницу к децибелам твоего эталона
        double targetDb = _baseVolumeDb + gain;

        // Ограничиваем сверху до 0 дБ, чтобы плеер не хрипел выше 100%
        if (targetDb > 0) targetDb = 0;

        // 3. Переводим децибелы обратно в проценты mpv
        double finalVolume = 100.0 * Math.Pow((targetDb / 60.0) + 1.0, 3.0);

        return Math.Round(finalVolume, 1);
    }

    public async Task<double> GetPlayerVolumeAsync()
    {
        if (_player == null)
            throw new Exception();

        JsonObject obj = await _player.SendRawRequestAsync("get_property", "volume");

        // TODO еррор смотреть но мне похуй

        return obj["data"].GetValue<double>();
    }

    public async Task UpdatePlayerVolumeAsync(double volume)
    {
        if (_player == null)
            throw new Exception();

        await _locker.WaitAsync();
        try
        {
            _logger.LogInformation("Поставили громкость {volume}%", volume);

            await _player.SendRawCommandAsync("set_property", "volume", volume);
        }
        finally
        {
            _locker.Release();
        }
    }

    public async Task UpdatePlayerVolumeAsync(SongHandler song, double volume)
    {
        if (_player == null)
            throw new Exception();

        await _locker.WaitAsync();
        try
        {
            if (_song != song)
                return;

            _logger.LogInformation("Поставили громкость {volume}%", volume);

            await _player.SendRawCommandAsync("set_property", "volume", volume);
        }
        finally
        {
            _locker.Release();
        }
    }

    public async Task UpdatePlayerFilterAsync(SongHandler song, string filter)
    {
        if (_player == null)
            throw new Exception();

        await _locker.WaitAsync();
        try
        {
            if (_song != song)
                return;

            _logger.LogInformation("Меняем фильтер на {filter}", filter);

            await _player.SendRawRequestAsync("set_property", "af", filter);
        }
        finally
        {
            _locker.Release();
        }
    }

    public async Task<SongHandler> SetPlayerTrackAsync(string filePath)
    {
        if (_player == null)
            throw new Exception();

        await _locker.WaitAsync();
        try
        {
            await _player.SendRawRequestAsync("loadfile", filePath, "replace");

            _song = new SongHandler(filePath);

            return _song;
        }
        finally
        {
            _locker.Release();
        }
    }

    public async Task<SongHandler> SetPlayerTrackAsync(string filePath, int index, string additionalArg)
    {
        if (_player == null)
            throw new Exception();

        await _locker.WaitAsync();
        try
        {
            await _player.SendRawRequestAsync("loadfile", filePath, "replace", index, additionalArg);

            _song = new SongHandler(filePath);

            return _song;
        }
        finally
        {
            _locker.Release();
        }
    }

    // public async Task AddVideoAsync(string filePath)
    // {
    //     if (_player == null)
    //         throw new Exception();
    //
    //     await _player.SendRawCommandAsync("video-add", filePath);
    // }

    // public async Task SetPlayerThingAsync(string filePath, double volume)
    // {
    //     if (_player == null)
    //         throw new Exception();
    //
    //     // https://mpv.io/manual/stable/#command-interface-[%3Coptions%3E]]]
    //
    //     // string escapedPath = UrlEncoder.Default.Encode(filePath);
    //
    //     // если ставить звук, когда трека ещё нет, это ничего не сделает
    //     await _player.SendRawRequestAsync("loadfile", filePath, "replace");
    //     await _player.SendRawCommandAsync("set_property", "volume", volume);
    // }

    public async Task CreateWindowAsync()
    {
        await _locker.WaitAsync();
        try
        {
            if (_player != null)
                throw new Exception();

            _player = MpvPlayerHandler.Create(_serviceProvider);
        }
        finally
        {
            _locker.Release();
        }

        await _player.StartAsync();
    }

    public void CloseWindow()
    {
        _locker.Wait();
        try
        {
            if (_player == null)
                throw new Exception();

            _player.Stop();
            _player = null;

            _song = null;
        }
        finally
        {
            _locker.Release();
        }
    }
}