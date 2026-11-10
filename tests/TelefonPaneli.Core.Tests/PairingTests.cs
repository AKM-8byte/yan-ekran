using Microsoft.Extensions.Time.Testing;
using TelefonPaneli.Core.Security;
using TelefonPaneli.Core.Settings;

namespace TelefonPaneli.Core.Tests;

public sealed class PairingTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("tp-").FullName;
    string SettingsPath => Path.Combine(_dir, "settings.json");
    public void Dispose() => Directory.Delete(_dir, true);

    readonly FakeTimeProvider _time = new();

    PairingService Make(params int[] codes)
    {
        var q = new Queue<int>(codes.Length > 0 ? codes : [1234, 5678, 9012, 3456]);
        return new PairingService(new SettingsStore(SettingsPath), () => q.Count > 1 ? q.Dequeue() : q.Peek(), _time);
    }

    [Fact]
    public void Correct_code_returns_token_and_rotates_code()
    {
        var p = Make(1234, 5678);
        Assert.Equal("1234", p.CurrentCode);
        string? changed = null;
        p.CodeChanged += c => changed = c;
        var r = p.TryPair("1234", "Telefon");
        Assert.Equal(PairStatus.Ok, r.Status);
        Assert.False(string.IsNullOrEmpty(r.Token));
        Assert.True(p.IsValid(r.Token));
        Assert.Equal("5678", p.CurrentCode);
        Assert.Equal("5678", changed);
    }

    [Fact]
    public void Wrong_code_is_rejected()
    {
        var p = Make();
        var r = p.TryPair("0000", "x");
        Assert.Equal(PairStatus.WrongCode, r.Status);
        Assert.Null(r.Token);
    }

    [Fact]
    public void Token_is_valid_and_survives_new_service_instance()
    {
        var token = Make().TryPair("1234", "Telefon").Token;
        Assert.True(Make().IsValid(token));
    }

    [Fact]
    public void Stored_token_is_hashed()
    {
        var token = Make().TryPair("1234", "Telefon").Token!;
        var json = File.ReadAllText(SettingsPath);
        Assert.DoesNotContain(token, json);
        Assert.Contains("Telefon", json);
    }

    [Fact]
    public void Five_wrong_codes_lock_and_rotate()
    {
        var p = Make(1234, 5678);
        for (var i = 0; i < 4; i++) Assert.Equal(PairStatus.WrongCode, p.TryPair("0000", "x").Status);
        Assert.Equal(PairStatus.Locked, p.TryPair("0000", "x").Status);
        Assert.Equal("5678", p.CurrentCode);
        Assert.Equal(PairStatus.Locked, p.TryPair("5678", "x").Status);
        _time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(PairStatus.Ok, p.TryPair("5678", "x").Status);
    }

    [Fact]
    public void ForgetAll_invalidates_existing_tokens()
    {
        var p = Make();
        var token = p.TryPair("1234", "Telefon").Token;
        var fired = false;
        p.DevicesForgotten += () => fired = true;
        p.ForgetAll();
        Assert.False(p.IsValid(token));
        Assert.False(Make().IsValid(token));
        Assert.True(fired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("rastgele")]
    public void Invalid_tokens_are_rejected(string? token) => Assert.False(Make().IsValid(token));

    [Fact]
    public void Code_keeps_leading_zeros() => Assert.Equal("0042", Make(42).CurrentCode);

    [Fact]
    public void Device_name_is_trimmed_and_limited()
    {
        Make().TryPair("1234", new string('a', 200));
        var s = new SettingsStore(SettingsPath).Load();
        Assert.Equal(40, s.Devices.Single().Name.Length);
    }
}
