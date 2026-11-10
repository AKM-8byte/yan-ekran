using Microsoft.Extensions.Time.Testing;
using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Core.Tests;

public class FpsTests
{
    readonly FakeTimeProvider _time = new();

    void Feed(FpsCalculator c, double ms, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(ms));
            c.AddFrame(ms);
        }
    }

    static string[] Fixture() => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "presentmon-sample.csv"));

    [Fact]
    public void Steady_144fps()
    {
        var c = new FpsCalculator(_time);
        Feed(c, 1000.0 / 144, 288);
        Assert.InRange(c.Fps!.Value, 143, 145);
    }

    [Fact]
    public void Low1_reflects_spikes()
    {
        var c = new FpsCalculator(_time);
        for (var i = 0; i < 1000; i++)
        {
            var ms = i % 100 == 0 ? 20 : 7;
            _time.Advance(TimeSpan.FromMilliseconds(ms));
            c.AddFrame(ms);
        }
        Assert.InRange(c.Low1!.Value, 48, 52);
        Assert.InRange(c.Fps!.Value, 130, 145);
    }

    [Fact]
    public void Old_frames_leave_the_low1_window()
    {
        var c = new FpsCalculator(_time);
        Feed(c, 50, 100);   // 5 sn, 20 fps
        Feed(c, 10, 1100);  // 11 sn, 100 fps
        Assert.InRange(c.Low1!.Value, 99, 101);
    }

    [Fact]
    public void No_frames_for_2s_gives_null()
    {
        var c = new FpsCalculator(_time);
        Feed(c, 10, 200);
        _time.Advance(TimeSpan.FromSeconds(2.1));
        Assert.Null(c.Fps);
        Assert.Null(c.Low1);
    }

    [Fact]
    public void Empty_calculator_is_null()
    {
        var c = new FpsCalculator(_time);
        Assert.Null(c.Fps);
        Assert.Null(c.Low1);
    }

    [Fact]
    public void Reset_clears()
    {
        var c = new FpsCalculator(_time);
        Feed(c, 10, 200);
        c.Reset();
        Assert.Null(c.Fps);
    }

    [Fact]
    public void Parses_fixture_by_header_name()
    {
        var lines = Fixture();
        var p = new PresentMonCsv();
        Assert.False(p.TryParse(lines[1], out _, out _)); // başlık okunmadan satır kabul edilmez
        Assert.True(p.TryReadHeader(lines[0]));
        Assert.True(p.TryParse(lines[1], out var name, out var ms));
        Assert.Equal("Game.exe", name);
        Assert.Equal(6.944, ms, 3);
    }

    [Fact]
    public void Header_match_is_case_insensitive_and_order_independent()
    {
        var p = new PresentMonCsv();
        Assert.True(p.TryReadHeader("TimeInSeconds,MsBetweenPresents,Application"));
        Assert.True(p.TryParse("1.0,16.6667,foo.exe", out var n, out var ms));
        Assert.Equal("foo.exe", n);
        Assert.Equal(16.6667, ms, 4);
    }

    [Theory]
    [InlineData("Game.exe,4242")]
    [InlineData("")]
    [InlineData("Game.exe,4242,0x1,DXGI,0,0,1,x,0,1.5,0.06,NaN,1,1,1")]
    [InlineData("Game.exe,4242,0x1,DXGI,0,0,1,x,0,1.5,0.06,abc,1,1,1")]
    [InlineData("Game.exe,4242,0x1,DXGI,0,0,1,x,0,1.5,0.06,-3,1,1,1")]
    public void Malformed_line_returns_false(string line)
    {
        var p = new PresentMonCsv();
        p.TryReadHeader(Fixture()[0]);
        Assert.False(p.TryParse(line, out _, out _));
    }

    [Fact]
    public void Header_without_column_is_rejected() => Assert.False(new PresentMonCsv().TryReadHeader("Application,ProcessID"));
}
