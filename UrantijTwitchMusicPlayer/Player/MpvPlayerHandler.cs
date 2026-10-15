using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace UrantijTwitchMusicPlayer.Player;

public class MpvPlayerHandler
{
    private const string SocketPath = "/tmp/mpv-socket";

    private readonly Process? _process;
    private readonly IServiceProvider _provider;
    private readonly ILogger _logger;

    private MpvSocketWrapper? _socket;

    private MpvPlayerHandler(Process? process, IServiceProvider provider, ILogger logger)
    {
        _process = process;
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

        _socket = await MpvSocketWrapper.CreateAsync(SocketPath, _provider);
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

        bool createProcess = true;
        if (mainOptions.Value.TryMpvExistingSocket)
        {
            if (File.Exists(SocketPath))
            {
                createProcess = false;
            }
        }

        Process? process = null;
        if (createProcess)
        {
            logger.LogInformation("Запускаем мпв");

            process = new Process();

            process.StartInfo.ArgumentList.Add($"--input-ipc-server={SocketPath}");
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
            new(process, provider, logger);

        return mpvPlayerHandler;
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        _logger.LogWarning("Мпв закрылся");
    }
}