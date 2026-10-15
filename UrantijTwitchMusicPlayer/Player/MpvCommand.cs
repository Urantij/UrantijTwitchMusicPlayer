using System.Text.Json.Serialization;

namespace UrantijTwitchMusicPlayer.Player;

public class MpvCommand
{
    [JsonPropertyName("command")] public object[] CommandArgs { get; }
    [JsonPropertyName("request_id")] public int? RequestId { get; }

    public MpvCommand(object[] commandArgs, int? requestId)
    {
        CommandArgs = commandArgs;
        RequestId = requestId;
    }
}