using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using UrantijTwitchMusicPlayer.Work;

namespace UrantijTwitchMusicPlayer.Routes;

public class AudioRoute
{
    public static async Task<IResult> PostSoundData(HttpContext httpContext, [FromServices] MpvWorker mpv,
        [FromServices] MainWorker main)
    {
        using StreamReader reader = new(httpContext.Request.Body);

        bool updateCache = false;
        {
            StringValues query = httpContext.Request.Query["update"];

            if (query.Count > 0 &&
                query.All(q => q?.Equals("false", StringComparison.OrdinalIgnoreCase) != true))
                updateCache = true;
        }
        bool useCurrent = false;
        {
            StringValues query = httpContext.Request.Query["current"];

            if (query.Count > 0 &&
                query.All(q => q?.Equals("false", StringComparison.OrdinalIgnoreCase) != true))
                useCurrent = true;
        }

        string data = await reader.ReadToEndAsync();

        double volume;
        double lufs;
        if (useCurrent)
        {
            double? currentLufs = main.CurrentTrackLufs;
            if (currentLufs == null)
            {
                return TypedResults.BadRequest("ща нет луфсов");
            }

            volume = await mpv.GetPlayerVolumeAsync();
            lufs = currentLufs.Value;
        }
        else
        {
            try
            {
                ReadOnlySpan<char> span = data.AsSpan();
                MemoryExtensions.SpanSplitEnumerator<char> split = span.Split(' ');

                split.MoveNext();
                volume = double.Parse(span[split.Current], CultureInfo.InvariantCulture);
                split.MoveNext();
                lufs = double.Parse(span[split.Current], CultureInfo.InvariantCulture);
            }
            catch
            {
                return TypedResults.BadRequest(
                    "'55 -7.9' броу или типа того, твоё чет не парсится (кавычки не нужны ес чо");
            }
        }

        await mpv.UpdateSoundDataAsync(volume, lufs);

        if (updateCache)
        {
            double? currentLufs = main.CurrentTrackLufs;

            if (currentLufs == null)
            {
                return TypedResults.BadRequest("ну дату обновил, но ты учитывай, что трека нет.");
            }

            double newVolume = mpv.CalculateVolumePercentForLufs(currentLufs.Value);

            await mpv.UpdatePlayerVolumeAsync(newVolume);
        }

        return TypedResults.Ok();
    }

    public static async Task<IResult> PostTrack(HttpContext httpContext,
        [FromServices] MainWorker mainWorker,
        [FromServices] Linker linker,
        ILogger<AudioRoute> logger)
    {
        using StreamReader reader = new(httpContext.Request.Body);

        string data = await reader.ReadToEndAsync();

        LinkInfo? linkInfo = linker.MaybeLink(data);

        if (linkInfo == null)
        {
            return TypedResults.BadRequest("Линка не парсится броу");
        }

        string? behalfUsername = null;
        if (httpContext.Request.Query.TryGetValue("behalf", out StringValues behalfValue))
        {
            behalfUsername = behalfValue.First();
        }

        ApiMusicEvent ev = new ApiMusicEvent(linkInfo, behalfUsername);

        await mainWorker.AddRewardAsync(ev);

        return TypedResults.Ok();
    }
}