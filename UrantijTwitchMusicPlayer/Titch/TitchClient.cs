using Microsoft.Extensions.Options;
using TwitchSimpleLib.Chat;
using TwitchSimpleLib.Chat.Messages;

namespace UrantijTwitchMusicPlayer.Titch;

public class ChatUserData
{
    public string Username { get; }
    public string Id { get; }
    public DateTimeOffset LastSeenDate { get; set; }

    public ChatUserData(string username, string id, DateTimeOffset lastSeenDate)
    {
        Username = username;
        Id = id;
        LastSeenDate = lastSeenDate;
    }
}

public class TitchClient : IHostedService
{
    private readonly TimeSpan _chatKeepTime = TimeSpan.FromMinutes(3);
    private readonly TimeSpan _checkCooldown = TimeSpan.FromMinutes(3);

    private readonly ILogger _logger;

    private readonly TwitchChatClient _client;
    private readonly ChatAutoChannel _channel;
    private readonly string _rewardId;
    private readonly bool _use;

    private readonly List<ChatUserData> _chatUserDatas = [];

    private DateTimeOffset _lastCheckDate = DateTimeOffset.UtcNow;

    public event Action<TwitchPrivateMessage>? RewardMessageReceived;

    public TitchClient(IOptions<TitchConfig>? titchOptions, IOptions<MainConfig> mainOptions,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TitchClient>();

        _client = new TwitchChatClient(true, titchOptions?.Value.Opts ?? new TwitchChatClientOpts(), loggerFactory);

        _channel = _client.AddAutoJoinChannel(mainOptions.Value.Channel);

        _rewardId = mainOptions.Value.RewardId;
        _use = mainOptions.Value.UseTwitchChat;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _channel.PrivateMessageReceived += ChannelOnPrivateMessageReceived;

        return _client.ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.PrivateMessageReceived -= ChannelOnPrivateMessageReceived;

        _client.Close();

        return Task.CompletedTask;
    }

    public ChatUserData? FindUserByUsername(string username)
    {
        lock (_chatUserDatas)
        {
            return _chatUserDatas.FirstOrDefault(d => d.Username == username);
        }
    }

    private void ChannelOnPrivateMessageReceived(object? sender, TwitchPrivateMessage e)
    {
        lock (_chatUserDatas)
        {
            ChatUserData? data = _chatUserDatas.FirstOrDefault(c => c.Id == e.userId);

            if (data == null)
            {
                data = new ChatUserData(e.username, e.userId, DateTimeOffset.UtcNow);
                _chatUserDatas.Add(data);
            }
            else
            {
                data.LastSeenDate = DateTimeOffset.UtcNow;
            }

            CheckChatData();
        }

        if (!_use)
            return;

        if (e.customRewardId != _rewardId)
        {
            if (e.username != "urantij" || !e.text.StartsWith("=плей"))
                return;
        }

        RewardMessageReceived?.Invoke(e);
    }

    private void CheckChatData()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        TimeSpan passed = now - _lastCheckDate;

        if (passed < _checkCooldown)
            return;

        _lastCheckDate = now;

        List<ChatUserData> toRemove = [];
        foreach (ChatUserData data in _chatUserDatas)
        {
            if (now - data.LastSeenDate < _chatKeepTime)
                continue;

            toRemove.Add(data);
        }

        foreach (ChatUserData data in toRemove)
        {
            _chatUserDatas.Remove(data);
        }
    }
}