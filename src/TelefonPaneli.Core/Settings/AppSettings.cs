using TelefonPaneli.Core.Layout;

namespace TelefonPaneli.Core.Settings;

public sealed record DeviceToken(string Id, string Name, string TokenHash, DateTimeOffset Created);

public sealed record AppSettings(LayoutModel Layout, List<DeviceToken> Devices, List<string> FifaProcessNames)
{
    public static readonly IReadOnlyList<string> DefaultFifaNames = ["FIFA23.exe", "FIFA23_Trial.exe"];
    public static AppSettings CreateDefault() => new(DefaultLayout.Create(), [], DefaultFifaNames.ToList());
}
