using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TelefonPaneli.Windows.Games;

/// <summary>FIFA'nın çalışıp çalışmadığını ve ön plandaki pencerenin sürecini izler. Poll() 2 sn'de bir çağrılır.</summary>
public sealed class GameWatcher
{
    readonly Func<IEnumerable<string>> _processNames;
    readonly HashSet<string> _fifa;
    readonly Func<int?> _foreground;

    public GameWatcher(Func<IEnumerable<string>> processNames, IEnumerable<string> fifaNames, Func<int?> foregroundPid)
    {
        _processNames = processNames;
        _fifa = fifaNames.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _foreground = foregroundPid;
    }

    /// <summary>Gerçek Windows süreç listesi ve ön plan penceresiyle.</summary>
    public static GameWatcher ForWindows(IEnumerable<string> fifaNames) =>
        new(() => Process.GetProcesses().Select(p => { using (p) return p.ProcessName; }).ToList(), fifaNames, ReadForegroundPid);

    public bool FifaRunning { get; private set; }
    public int? ForegroundPid { get; private set; }

    public void Poll()
    {
        try { FifaRunning = _processNames().Any(n => _fifa.Contains(Normalize(n))); }
        catch (Exception) { }
        try { ForegroundPid = _foreground(); }
        catch (Exception) { ForegroundPid = null; }
    }

    static string Normalize(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    static int? ReadForegroundPid()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == 0 ? null : (int)pid;
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
