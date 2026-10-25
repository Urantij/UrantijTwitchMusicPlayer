using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UrantijTwitchMusicPlayer.Player;

[JsonSerializable(typeof(MpvCommand))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
internal partial class MpvJsonContext : JsonSerializerContext;

// https://github.com/mpv-player/mpv/blob/master/DOCS/man/ipc.rst

public class MpvSocketWrapper : IDisposable
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly StreamWriter _writer;
    private readonly Task _listenTask;
    private readonly ILogger _logger;

    private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(10);

    private readonly Lock _locker = new();
    private readonly Dictionary<int, TaskCompletionSource<JsonObject>> _dict = [];

    private bool _isDisposed;

    // TODO прикинь 2кк запросов сделать ууух
    private int _nextRequestId = 1;

    private MpvSocketWrapper(Socket socket, NetworkStream stream, StreamWriter writer, ILogger logger)
    {
        _socket = socket;
        _stream = stream;
        _writer = writer;
        _logger = logger;

        // не оч это мне нра каеш
        _listenTask = Task.Run(ListenToMpvAsync);
    }

    public static async Task<MpvSocketWrapper> CreateAsync(string socketPath, IServiceProvider serviceProvider)
    {
        Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        UnixDomainSocketEndPoint endpoint = new(socketPath);

        await socket.ConnectAsync(endpoint);

        NetworkStream stream = new(socket);
        StreamWriter writer = new(stream, new UTF8Encoding(false)) { AutoFlush = true };

        ILogger logger = serviceProvider.GetRequiredService<ILogger<MpvSocketWrapper>>();

        return new MpvSocketWrapper(socket, stream, writer, logger);
    }

    public async Task SendCommandAsync(params object[] args)
    {
        MpvCommand payload = new MpvCommand(args, null);
        string json = JsonSerializer.Serialize(payload, payload.GetType(), MpvJsonContext.Default);

        await _writer.WriteLineAsync(json);
    }

    public async Task<JsonObject> SendRequestAsync(params object[] args)
    {
        int requestId = GetNewRequestId();

        TaskCompletionSource<JsonObject> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_locker)
        {
            _dict[requestId] = tcs;
        }

        MpvCommand payload = new MpvCommand(args, requestId);
        string json = JsonSerializer.Serialize(payload, payload.GetType(), MpvJsonContext.Default);

        await _writer.WriteLineAsync(json);

        // TODO это всё полная ХУЙНЯ
        CancellationTokenSource cts = new();
        // не хочу, там заёб со слежкой.
        // cts.CancelAfter(_requestTimeout);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_requestTimeout, cts.Token);
            }
            catch
            {
                cts.Dispose();
                return;
            }

            lock (_locker)
            {
                if (!_dict.Remove(requestId))
                {
                    // всё в поряде, оно просто проскочило
                    cts.Dispose();
                    return;
                }
            }

            cts.Dispose();
            tcs.SetCanceled();
        });

        JsonObject result = await tcs.Task;

        await cts.CancelAsync();

        return result;
    }

    private async Task ListenToMpvAsync()
    {
        using var reader = new StreamReader(_stream, Encoding.UTF8);
        try
        {
            while (!_isDisposed)
            {
                string? line = await reader.ReadLineAsync();

                if (line == null)
                {
                    _logger.LogInformation("мпв закончил разговор");
                    return;
                }

                JsonObject obj = (JsonObject)JsonNode.Parse(line);

                JsonNode? requestIdNode = obj["request_id"];

                if (requestIdNode == null)
                {
                    _logger.LogDebug("Сообщение от мпв {msg}", line);
                    continue;
                }

                int requestId = requestIdNode.GetValue<int>();

                TaskCompletionSource<JsonObject>? tcs;
                lock (_locker)
                {
                    if (!_dict.Remove(requestId, out tcs))
                    {
                        _logger.LogWarning("Пришёл ответ, который не ждут {id}", requestId);
                        continue;
                    }
                }

                tcs.SetResult(obj);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Чтение мпв");
        }
    }

    private int GetNewRequestId()
    {
        return _nextRequestId++;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _writer.Dispose();
        _stream.Dispose();
        _socket.Dispose();
    }

    // private void Test()
    // {
    //     using var client = new NamedPipeClientStream(".", "mpv-pipe", PipeDirection.InOut);
    //     await client.ConnectAsync();
    // }
}