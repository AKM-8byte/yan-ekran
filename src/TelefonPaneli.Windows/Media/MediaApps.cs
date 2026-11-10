namespace TelefonPaneli.Windows.Media;

/// <summary>SMTC kaynak kimliğinden (AUMID) görünen ad ve ses oturumu için süreç adı çıkarır.</summary>
public static class MediaApps
{
    static readonly string[] Browsers = ["chrome", "msedge"];

    public static string AppName(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return "Medya";
        if (aumid.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (IsBrowser(aumid)) return "YouTube Music";
        var name = aumid.Contains('!') ? aumid[(aumid.LastIndexOf('!') + 1)..] : aumid;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    /// <summary>Ses karıştırıcıda aranacak süreç adı (uzantısız, küçük harf). Bilinmiyorsa null.</summary>
    public static string? ProcessName(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return null;
        if (aumid.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "spotify";
        foreach (var b in Browsers)
            if (aumid.StartsWith(b, StringComparison.OrdinalIgnoreCase)) return b;
        if (aumid.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return aumid[..^4].ToLowerInvariant();
        return null;
    }

    static bool IsBrowser(string aumid) => Browsers.Any(b => aumid.StartsWith(b, StringComparison.OrdinalIgnoreCase));
}
