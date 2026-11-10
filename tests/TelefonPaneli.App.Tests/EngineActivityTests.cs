using TelefonPaneli.Windows.Sensors;

namespace TelefonPaneli.App.Tests;

public class EngineActivityTests
{
    const string Luid = "luid_0x00000000_0x0000D1B3";
    static (string, long) S(string pid, long raw, string luid = Luid) => ($"pid_{pid}_{luid}_phys_0_eng_0_engtype_3D", raw);

    [Fact]
    public void First_sample_is_not_activity()
        => Assert.False(new EngineActivity(Luid).Update([S("1", 5_000_000)]));

    [Fact]
    public void Static_cumulative_value_is_idle()
    {
        // Sayaç Timer100Ns türünde: değer birikimli. Değişmiyorsa motor boşta.
        var a = new EngineActivity(Luid);
        a.Update([S("1", 5_000_000)]);
        Assert.False(a.Update([S("1", 5_000_000)]));
    }

    [Fact]
    public void Increasing_value_is_activity()
    {
        var a = new EngineActivity(Luid);
        a.Update([S("1", 5_000_000)]);
        Assert.True(a.Update([S("1", 5_100_000)]));
    }

    [Fact]
    public void Other_adapter_is_ignored()
    {
        var a = new EngineActivity(Luid);
        a.Update([S("1", 1, "luid_0x00000000_0x0000AAAA")]);
        Assert.False(a.Update([S("1", 999, "luid_0x00000000_0x0000AAAA")]));
    }

    [Fact]
    public void New_instance_with_nonzero_value_is_not_activity_until_it_grows()
    {
        var a = new EngineActivity(Luid);
        a.Update([S("1", 10)]);
        Assert.False(a.Update([S("1", 10), S("2", 7_000_000)]));
        Assert.True(a.Update([S("1", 10), S("2", 7_000_001)]));
    }
}
