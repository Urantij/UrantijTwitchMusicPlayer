using System.Text.Json.Serialization;
using TwitchSimpleLib.Chat;

namespace UrantijTwitchMusicPlayer.Titch;

[JsonSerializable(typeof(TitchConfig))]
internal partial class TitchConfigSerializationContext : JsonSerializerContext;

public class TitchConfig
{
    public TwitchChatClientOpts Opts { get; set; }
}