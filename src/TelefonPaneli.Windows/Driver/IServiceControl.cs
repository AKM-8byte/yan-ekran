using System.Diagnostics;
using System.ServiceProcess;

namespace TelefonPaneli.Windows.Driver;

public enum ServiceState { Stopped, Running, Other }

public interface IServiceControl
{
    /// <summary>Servis yoksa null.</summary>
    ServiceState? Query(string name);
    void Start(string name);
    void Stop(string name);
    void SetManualStart(string name);
}

/// <summary>Gerçek Windows servis denetimi (yönetici yetkisi gerekir).</summary>
public sealed class WindowsServiceControl : IServiceControl
{
    static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    public ServiceState? Query(string name)
    {
        using var sc = Find(name);
        if (sc is null) return null;
        return sc.Status switch
        {
            ServiceControllerStatus.Stopped => ServiceState.Stopped,
            ServiceControllerStatus.Running => ServiceState.Running,
            _ => ServiceState.Other,
        };
    }

    public void Start(string name)
    {
        using var sc = Find(name) ?? throw new InvalidOperationException(name + " yok");
        if (sc.Status != ServiceControllerStatus.Running) { sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, Wait); }
    }

    public void Stop(string name)
    {
        using var sc = Find(name);
        if (sc is null || sc.Status == ServiceControllerStatus.Stopped) return;
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, Wait);
    }

    public void SetManualStart(string name)
    {
        using var p = Process.Start(new ProcessStartInfo("sc.exe", $"config {name} start= demand")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        p.WaitForExit(5000);
    }

    static ServiceController? Find(string name) =>
        ServiceController.GetDevices().Concat(ServiceController.GetServices())
            .FirstOrDefault(s => s.ServiceName.Equals(name, StringComparison.OrdinalIgnoreCase));
}
