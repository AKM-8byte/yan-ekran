using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Core.Tests;

public class MetricHubTests
{
    sealed class Fixed(string id, double? value) : IMetricSource
    {
        public string Name => "fixed:" + id;
        public void Collect(IDictionary<string, double?> into) => into[id] = value;
    }

    sealed class Throwing : IMetricSource
    {
        public string Name => "throwing";
        public void Collect(IDictionary<string, double?> into) => throw new InvalidOperationException("sensör yok");
    }

    sealed class FlakyFan : IMetricSource
    {
        public bool Fail;
        public string Name => "fan";
        public void Collect(IDictionary<string, double?> into)
        {
            if (Fail) throw new InvalidOperationException();
            into["fan.0"] = 1450;
        }
    }

    static HubFlags Normal() => new("normal", false, false);

    [Fact]
    public void Collect_merges_all_sources()
    {
        var hub = new MetricHub(new IMetricSource[] { new Fixed("cpu.load", 34), new Fixed("gpu.temp", 78) }, Normal);
        var s = hub.Collect();
        Assert.Equal(34, s.Metrics["cpu.load"]);
        Assert.Equal(78, s.Metrics["gpu.temp"]);
    }

    [Fact]
    public void Collect_carries_flags()
    {
        var hub = new MetricHub(Array.Empty<IMetricSource>(), () => new HubFlags("fifa", true, true));
        var s = hub.Collect();
        Assert.Equal("fifa", s.Mode);
        Assert.True(s.GpuAsleep);
        Assert.True(s.FifaWarning);
    }

    [Fact]
    public void Throwing_source_does_not_break_others()
    {
        var hub = new MetricHub(new IMetricSource[] { new Throwing(), new Fixed("cpu.load", 34) }, Normal);
        var s = hub.Collect();
        Assert.Equal(34, s.Metrics["cpu.load"]);
    }

    [Fact]
    public void Throwing_source_keeps_its_last_keys_as_null()
    {
        var fan = new FlakyFan();
        var hub = new MetricHub(new IMetricSource[] { fan }, Normal);
        Assert.Equal(1450, hub.Collect().Metrics["fan.0"]);
        fan.Fail = true;
        var s = hub.Collect();
        Assert.True(s.Metrics.ContainsKey("fan.0"));
        Assert.Null(s.Metrics["fan.0"]);
    }

    [Theory]
    [InlineData("cpu.temp", true)]
    [InlineData("cpu.clock", true)]
    [InlineData("fan.3", true)]
    [InlineData("disk.1.temp", true)]
    [InlineData("gpu.temp", false)]
    [InlineData("fps", false)]
    [InlineData("cpu.load", false)]
    public void RequiresDriver(string id, bool expected) => Assert.Equal(expected, MetricIds.RequiresDriver(id));
}
