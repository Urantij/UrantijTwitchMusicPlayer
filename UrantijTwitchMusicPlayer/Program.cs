using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrantijTwitchMusicPlayer.Data;
using UrantijTwitchMusicPlayer.Routes;
using UrantijTwitchMusicPlayer.Shared.Data;
using UrantijTwitchMusicPlayer.Titch;
using UrantijTwitchMusicPlayer.Work;
using UrantijTwitchMusicPlayer.Work.Ffmpeg;
using UrantijTwitchMusicPlayer.Work.Links;
using UrantijTwitchMusicPlayer.Work.Yt;

namespace UrantijTwitchMusicPlayer;

class Program
{
    static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(c => { c.TimestampFormat = "[HH:mm:ss] "; });

        builder.Services.AddDbContextFactory<MyContext>(optionsBuilder =>
        {
            optionsBuilder.UseSqlite(builder.Configuration.GetConnectionString("db") ?? $"Data Source=db.sqlite;");
            // этого друга нужно закомментить перед первой CompiledModels компиляции, удачи!
            optionsBuilder.UseModel(UrantijTwitchMusicPlayer.CompiledModels.MyContextModel.Instance);
        });

        // Очень жаль, но нормального способа читать конфиги в aot тупо нет.
        {
            string settingsContent = File.ReadAllText("./appsettings.json");

            JsonNode? node = JsonNode.Parse(settingsContent)?["Twitch"];
            TitchConfig? config =
                node?.Deserialize(typeof(TitchConfig), TitchConfigSerializationContext.Default) as TitchConfig;
            if (config != null)
            {
                OptionsWrapper<TitchConfig> optionsWrapper = new(config);

                builder.Services.AddSingleton<IOptions<TitchConfig>>(_ => optionsWrapper);
            }
        }
        {
            string settingsContent = File.ReadAllText("./appsettings.json");

            JsonNode? node = JsonNode.Parse(settingsContent)?["Main"];
            MainConfig? config =
                node?.Deserialize(typeof(MainConfig), MainConfigSerializationContext.Default) as MainConfig;
            if (config == null)
                throw new Exception("мейн конфига нет блин");

            OptionsWrapper<MainConfig> optionsWrapper = new(config);

            builder.Services.AddSingleton<IOptions<MainConfig>>(_ => optionsWrapper);
        }

        builder.Services.AddSingleton<Database>();
        builder.Services.AddSingleton<Linker>();
        builder.Services.AddSingleton<FfmpegWorker>();
        builder.Services.AddSingleton<YtDlpWorker>();
        builder.Services.AddSingleton<CacheWorker>();
        builder.Services.AddSingleton<Piper>();

        builder.Services.AddSingleton<MpvWorker>();
        builder.Services.AddHostedService<MpvWorker>(p => p.GetRequiredService<MpvWorker>());

        builder.Services.AddSingleton<TitchClient>();
        builder.Services.AddHostedService<TitchClient>(p => p.GetRequiredService<TitchClient>());

        builder.Services.AddSingleton<MainWorker>();
        builder.Services.AddHostedService<MainWorker>(p => p.GetRequiredService<MainWorker>());

        // builder.Services.AddSingleton<MyConsole>();
        // builder.Services.AddHostedService<MyConsole>(p => p.GetRequiredService<MyConsole>());

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolver = MyJsonSirContext.Default;
        });

        var host = builder.Build();

        host.MapPost("/private/audio/track", AudioRoute.PostTrack);
        host.MapPost("/private/audio/sounddata", AudioRoute.PostSoundData);

        using (IServiceScope provider = host.Services.CreateScope())
        {
            ILogger logger = provider.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("info");
            logger.LogDebug("debug");

            if (!File.Exists("./efbundle"))
            {
                throw new Exception("efbundle не найден");
            }

            using Process process = new();

            string? connectionString =
                provider.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("db");
            if (connectionString != null)
            {
                process.StartInfo.ArgumentList.Add("--connection");
                process.StartInfo.ArgumentList.Add(connectionString);
            }

            process.StartInfo.FileName = "./efbundle";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            process.WaitForExit();

            int code = process.ExitCode;

            if (code != 0)
                throw new Exception($"Код выхода миграции {code}");
        }

        host.Run();
    }
}