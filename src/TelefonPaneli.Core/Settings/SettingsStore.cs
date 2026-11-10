using System.Text.Json;
using TelefonPaneli.Core.Layout;

namespace TelefonPaneli.Core.Settings;

/// <summary>%AppData%\TelefonPaneli\settings.json okuma/yazma. Bozuk dosya .bak olarak saklanır.</summary>
public sealed class SettingsStore(string path)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    readonly object _gate = new();

    public string FilePath => path;

    public static string DefaultPath() =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TelefonPaneli", "settings.json");

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(path)) return AppSettings.CreateDefault();
            try
            {
                var raw = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json)
                          ?? throw new JsonException("boş");
                return Normalize(raw);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                File.Copy(path, path + ".bak", overwrite: true);
                return AppSettings.CreateDefault();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
            File.Move(tmp, path, overwrite: true);
        }
    }

    static AppSettings Normalize(AppSettings s) => new(
        LayoutValidator.Sanitize(s.Layout),
        s.Devices?.Where(d => d is not null && !string.IsNullOrEmpty(d.TokenHash)).ToList() ?? [],
        s.FifaProcessNames is { Count: > 0 } names ? names : AppSettings.DefaultFifaNames.ToList());
}
