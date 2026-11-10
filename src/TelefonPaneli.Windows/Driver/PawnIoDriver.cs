namespace TelefonPaneli.Windows.Driver;

public enum DriverStatus { Ready, NotInstalled, DisabledForFifa, Failed }

/// <summary>
/// PawnIO çekirdek sürücüsünün yaşam döngüsü. Sürücü yalnızca normal modda ve program açıkken çalışır;
/// FIFA modunda ve program kapanınca durdurulur. Başlangıç türü her zaman "manual" yapılır ki
/// Windows açılışında kendiliğinden yüklenmesin.
/// </summary>
public sealed class PawnIoDriver(IServiceControl sc)
{
    public const string ServiceName = "PawnIO";

    public DriverStatus Prepare(bool fifaMode)
    {
        try
        {
            var state = sc.Query(ServiceName);
            if (state is null) return DriverStatus.NotInstalled;
            sc.SetManualStart(ServiceName);
            if (fifaMode)
            {
                if (state != ServiceState.Stopped) sc.Stop(ServiceName);
                return DriverStatus.DisabledForFifa;
            }
            if (state != ServiceState.Running) sc.Start(ServiceName);
            return DriverStatus.Ready;
        }
        catch (Exception)
        {
            return DriverStatus.Failed;
        }
    }

    public void Shutdown()
    {
        try { sc.Stop(ServiceName); }
        catch (Exception) { }
    }
}
