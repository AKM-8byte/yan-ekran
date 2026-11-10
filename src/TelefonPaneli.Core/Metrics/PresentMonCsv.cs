using System.Globalization;

namespace TelefonPaneli.Core.Metrics;

/// <summary>PresentMon CSV çıktısını okur. Sütunlar başlıktaki ada göre bulunur (büyük/küçük harf duyarsız).</summary>
public sealed class PresentMonCsv
{
    int _app = -1, _ms = -1;

    public bool TryReadHeader(string line)
    {
        var cols = line.Split(',');
        var app = Array.FindIndex(cols, c => c.Trim().Equals("Application", StringComparison.OrdinalIgnoreCase));
        var ms = Array.FindIndex(cols, c => c.Trim().Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase));
        if (app < 0 || ms < 0) return false;
        (_app, _ms) = (app, ms);
        return true;
    }

    public bool TryParse(string line, out string processName, out double ms)
    {
        processName = ""; ms = 0;
        if (_app < 0 || string.IsNullOrEmpty(line)) return false;
        var cols = line.Split(',');
        if (cols.Length <= Math.Max(_app, _ms)) return false;
        if (!double.TryParse(cols[_ms], NumberStyles.Float, CultureInfo.InvariantCulture, out ms)
            || !double.IsFinite(ms) || ms <= 0) return false;
        processName = cols[_app].Trim();
        return processName.Length > 0;
    }
}
