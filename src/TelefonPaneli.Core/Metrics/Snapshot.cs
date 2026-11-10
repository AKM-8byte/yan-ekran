namespace TelefonPaneli.Core.Metrics;

public sealed record HubFlags(string Mode, bool GpuAsleep, bool FifaWarning);

public sealed record Snapshot(string Mode, bool GpuAsleep, bool FifaWarning, IReadOnlyDictionary<string, double?> Metrics);
