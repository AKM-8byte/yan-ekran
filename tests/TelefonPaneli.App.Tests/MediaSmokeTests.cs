using TelefonPaneli.Windows.Media;
using Xunit.Abstractions;

namespace TelefonPaneli.App.Tests;

[Trait("Category", "Hardware")]
public class MediaSmokeTests(ITestOutputHelper output)
{
    [HardwareFact]
    public async Task Smtc_reads_current_media_without_throwing()
    {
        using var src = await SmtcMediaSource.CreateAsync(new AppVolume());
        await Task.Delay(500);
        var s = src.Current;
        output.WriteLine(s is null ? "çalan yok" : $"{s.App} | {s.Title} | {s.Artist} | çalıyor={s.IsPlaying} | {s.PositionMs}/{s.DurationMs} | ses={s.Volume}");
        var art = await src.GetArtAsync();
        output.WriteLine("kapak bayt: " + (art?.Length ?? 0));
    }
}
