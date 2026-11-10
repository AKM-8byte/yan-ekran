using TelefonPaneli.Windows.Driver;
using TelefonPaneli.Windows.Games;

namespace TelefonPaneli.App.Tests;

sealed class FakeServices : IServiceControl
{
    public ServiceState? State = ServiceState.Stopped;
    public List<string> Calls { get; } = [];
    public ServiceState? Query(string name) { Calls.Add("query:" + name); return State; }
    public void Start(string name) { Calls.Add("start"); State = ServiceState.Running; }
    public void Stop(string name) { Calls.Add("stop"); State = ServiceState.Stopped; }
    public void SetManualStart(string name) => Calls.Add("manual");
}

public class PawnIoDriverTests
{
    [Fact]
    public void Normal_mode_sets_manual_and_starts()
    {
        var sc = new FakeServices();
        Assert.Equal(DriverStatus.Ready, new PawnIoDriver(sc).Prepare(fifaMode: false));
        Assert.Equal(["query:PawnIO", "manual", "start"], sc.Calls);
    }

    [Fact]
    public void Normal_mode_does_not_restart_running_service()
    {
        var sc = new FakeServices { State = ServiceState.Running };
        new PawnIoDriver(sc).Prepare(false);
        Assert.DoesNotContain("start", sc.Calls);
    }

    [Fact]
    public void Fifa_mode_stops_running_service_and_never_starts()
    {
        var sc = new FakeServices { State = ServiceState.Running };
        Assert.Equal(DriverStatus.DisabledForFifa, new PawnIoDriver(sc).Prepare(fifaMode: true));
        Assert.Contains("manual", sc.Calls);
        Assert.Contains("stop", sc.Calls);
        Assert.DoesNotContain("start", sc.Calls);
        Assert.Equal(ServiceState.Stopped, sc.State);
    }

    [Fact]
    public void Missing_service_returns_NotInstalled_without_throwing()
    {
        var sc = new FakeServices { State = null };
        Assert.Equal(DriverStatus.NotInstalled, new PawnIoDriver(sc).Prepare(false));
        Assert.Equal(DriverStatus.NotInstalled, new PawnIoDriver(sc).Prepare(true));
        Assert.DoesNotContain("start", sc.Calls);
    }

    [Fact]
    public void Shutdown_stops_service()
    {
        var sc = new FakeServices();
        var d = new PawnIoDriver(sc);
        d.Prepare(false);
        d.Shutdown();
        Assert.Equal(ServiceState.Stopped, sc.State);
    }

    [Fact]
    public void Start_failure_is_reported_not_thrown()
    {
        var sc = new ThrowingStart();
        Assert.Equal(DriverStatus.Failed, new PawnIoDriver(sc).Prepare(false));
    }

    [Fact]
    public void Fifa_mode_stop_failure_is_reported_as_FifaStopFailed()
    {
        var sc = new StuckStop();
        Assert.Equal(DriverStatus.FifaStopFailed, new PawnIoDriver(sc).Prepare(fifaMode: true));
    }

    [Fact]
    public void Fifa_mode_stop_exception_is_reported_as_FifaStopFailed()
    {
        var sc = new ThrowingStop();
        Assert.Equal(DriverStatus.FifaStopFailed, new PawnIoDriver(sc).Prepare(fifaMode: true));
    }

    [Fact]
    public void Fifa_mode_retries_while_pending_then_succeeds()
    {
        var sc = new SlowStop();
        Assert.Equal(DriverStatus.DisabledForFifa, new PawnIoDriver(sc, retryDelay: TimeSpan.Zero).Prepare(fifaMode: true));
        Assert.True(sc.StopCalls >= 2);
    }

    /// Stop çağrılsa da servis RUNNING kalır (başka bir program tutamaç tutuyor).
    sealed class StuckStop : IServiceControl
    {
        public ServiceState? Query(string name) => ServiceState.Running;
        public void Start(string name) { }
        public void Stop(string name) { }
        public void SetManualStart(string name) { }
    }

    sealed class ThrowingStop : IServiceControl
    {
        public ServiceState? Query(string name) => ServiceState.Running;
        public void Start(string name) { }
        public void Stop(string name) => throw new InvalidOperationException("zaman aşımı");
        public void SetManualStart(string name) { }
    }

    /// İlk durdurma isteğinde STOP_PENDING (Other) kalır, ikincide durur.
    sealed class SlowStop : IServiceControl
    {
        public int StopCalls;
        ServiceState _s = ServiceState.Running;
        public ServiceState? Query(string name) => _s;
        public void Start(string name) { }
        public void Stop(string name) { StopCalls++; _s = StopCalls >= 2 ? ServiceState.Stopped : ServiceState.Other; }
        public void SetManualStart(string name) { }
    }

    sealed class ThrowingStart : IServiceControl
    {
        public ServiceState? Query(string name) => ServiceState.Stopped;
        public void Start(string name) => throw new InvalidOperationException("erişim reddedildi");
        public void Stop(string name) { }
        public void SetManualStart(string name) { }
    }
}

public class GameWatcherTests
{
    [Fact]
    public void Fifa_detected_case_insensitive()
    {
        var names = new List<string> { "explorer", "chrome" };
        var w = new GameWatcher(() => names, ["FIFA23.exe"], () => null);
        w.Poll();
        Assert.False(w.FifaRunning);
        names.Add("fifa23"); // Process.ProcessName uzantısız gelir
        w.Poll();
        Assert.True(w.FifaRunning);
    }

    [Fact]
    public void Foreground_pid_is_reported()
    {
        var w = new GameWatcher(() => [], ["FIFA23.exe"], () => 4242);
        w.Poll();
        Assert.Equal(4242, w.ForegroundPid);
    }

    [Fact]
    public void Process_enumeration_failure_keeps_last_state()
    {
        var fail = false;
        var w = new GameWatcher(() => fail ? throw new InvalidOperationException() : ["FIFA23"], ["FIFA23.exe"], () => null);
        w.Poll();
        fail = true;
        w.Poll();
        Assert.True(w.FifaRunning);
    }
}
