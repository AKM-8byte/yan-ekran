using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace TelefonPaneli.Windows.Media;

/// <summary>
/// Yalnızca belirli bir uygulamanın (Spotify, Chrome…) ses düzeyini okur/yazar.
/// Windows ana sesine (endpoint volume) asla dokunmaz.
/// </summary>
public sealed class AppVolume : IDisposable
{
    readonly object _gate = new();

    public double? Get(string aumid)
    {
        var sessions = Sessions(aumid);
        return sessions.Count == 0 ? null : sessions.Average(s => s.SimpleAudioVolume.Volume);
    }

    public void Set(string aumid, double value)
    {
        var v = (float)Math.Clamp(value, 0, 1);
        foreach (var s in Sessions(aumid)) s.SimpleAudioVolume.Volume = v;
    }

    List<AudioSessionControl> Sessions(string aumid)
    {
        var target = MediaApps.ProcessName(aumid);
        var result = new List<AudioSessionControl>();
        if (target is null) return result;
        lock (_gate)
        {
            try
            {
                // Ana iş parçacığı STA; numaralandırıcı her çağrıda çağıranın (iş parçacığı havuzu, MTA) üzerinde oluşturulur.
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = device.AudioSessionManager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    if (s.IsSystemSoundsSession) continue;
                    if (ProcessNameOf((int)s.GetProcessID) is { } name && name.Equals(target, StringComparison.OrdinalIgnoreCase))
                        result.Add(s);
                }
            }
            catch (Exception) { }
        }
        return result;
    }

    static string? ProcessNameOf(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch (Exception) { return null; }
    }

    public void Dispose() { }
}
