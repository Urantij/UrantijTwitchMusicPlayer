using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Player;

public class MpvPlayerHandler
{
    private readonly Process? _process;
    private readonly string _socketPath;
    private readonly IServiceProvider _provider;
    private readonly ILogger _logger;

    private MpvSocketWrapper? _socket;

    private MpvPlayerHandler(Process? process, string socketPath, IServiceProvider provider,
        ILogger<MpvPlayerHandler> logger)
    {
        _process = process;
        _socketPath = socketPath;
        _provider = provider;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_process != null)
        {
            _process.Exited += ProcessOnExited;
            _process.Start();
        }

        await Task.Delay(1000);

        _socket = await MpvSocketWrapper.CreateAsync(_socketPath, _provider);
    }

    public void Stop()
    {
        _socket?.Dispose();

        _process?.Close();
    }

    public Task SendRawCommandAsync(params object[] arguments)
    {
        if (_socket == null)
            throw new Exception();

        return _socket.SendCommandAsync(arguments);
    }

    public Task<JsonObject> SendRawRequestAsync(params object[] arguments)
    {
        if (_socket == null)
            throw new Exception();

        return _socket.SendRequestAsync(arguments);
    }

    public static MpvPlayerHandler Create(IServiceProvider provider)
    {
        IOptions<MainConfig> mainOptions = provider.GetRequiredService<IOptions<MainConfig>>();
        ILogger<MpvPlayerHandler> logger = provider.GetRequiredService<ILogger<MpvPlayerHandler>>();

        string socketPath = mainOptions.Value.MpvSocketPath;

        bool createProcess = true;
        if (mainOptions.Value.TryMpvExistingSocket)
        {
            if (File.Exists(socketPath))
            {
                createProcess = false;
            }
        }

        Process? process = null;
        if (createProcess)
        {
            logger.LogInformation("Запускаем мпв");

            process = new Process();

            process.StartInfo.ArgumentList.Add($"--input-ipc-server={socketPath}");
            process.StartInfo.ArgumentList.Add("--keep-open");
            process.StartInfo.ArgumentList.Add("--idle");
            process.StartInfo.ArgumentList.Add("--force-window");
            process.StartInfo.ArgumentList.Add("--autofit=500x500");
            process.StartInfo.FileName = "mpv";
        }
        else
        {
            logger.LogInformation("Не запускаем мпв :)");
        }

        MpvPlayerHandler mpvPlayerHandler =
            new(process, socketPath, provider, logger);

        return mpvPlayerHandler;
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        _logger.LogWarning("Мпв закрылся");
    }
}