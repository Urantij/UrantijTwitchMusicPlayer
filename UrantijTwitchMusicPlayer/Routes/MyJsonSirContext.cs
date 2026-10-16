using System.Text.Json.Serialization;

namespace UrantijTwitchMusicPlayer.Routes;

[JsonSerializable(typeof(string))]
internal partial class MyJsonSirContext : JsonSerializerContext;