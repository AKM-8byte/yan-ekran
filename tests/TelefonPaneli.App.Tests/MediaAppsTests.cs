using TelefonPaneli.Windows.Media;

namespace TelefonPaneli.App.Tests;

public class MediaAppsTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("Chrome", "YouTube Music")]
    [InlineData("chrome.exe", "YouTube Music")]
    [InlineData("MSEdge", "YouTube Music")]
    [InlineData("Chrome._crx_cinhimbnkkaeohfgghhklpknlkffjgod", "YouTube Music")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Microsoft.ZuneMusic")]
    [InlineData("vlc.exe", "vlc")]
    [InlineData("", "Medya")]
    public void AppName(string aumid, string expected) => Assert.Equal(expected, MediaApps.AppName(aumid));

    [Theory]
    [InlineData("Spotify.exe", "spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "spotify")]
    [InlineData("Chrome", "chrome")]
    [InlineData("Chrome._crx_cinhimbnkkaeohfgghhklpknlkffjgod", "chrome")]
    [InlineData("MSEdge", "msedge")]
    [InlineData("vlc.exe", "vlc")]
    public void ProcessName(string aumid, string expected) => Assert.Equal(expected, MediaApps.ProcessName(aumid));

    [Fact]
    public void Unknown_store_app_has_no_process_name() => Assert.Null(MediaApps.ProcessName("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic"));
}
