using System.Diagnostics;

namespace TelefonPaneli.App.Setup;

/// <summary>Telefonun bağlanabilmesi için özel (ev) ağ profilinde gelen TCP 5050 izni.</summary>
public static class Firewall
{
    const string RuleName = "TelefonPaneli";

    public static void EnsureRule(int port)
    {
        try
        {
            if (Netsh($"advfirewall firewall show rule name=\"{RuleName}\"") == 0) return;
            Netsh($"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport={port} profile=private");
        }
        catch (Exception) { /* yetki yoksa kullanıcı Windows'un sorduğu pencereden izin verir */ }
    }

    static int Netsh(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("netsh.exe", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return p.ExitCode;
    }
}
