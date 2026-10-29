using System.Text.Json.Serialization;
using UrantijTwitchMusicPlayer.Work.Leaderboard;

namespace UrantijTwitchMusicPlayer.Routes;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Leaderboard))]
internal partial class MyJsonSirContext : JsonSerializerContext;