namespace TelefonPaneli.Windows.Driver;

public enum DriverStatus { Ready, NotInstalled, DisabledForFifa, Failed, FifaStopFailed }

/// <summary>
/// PawnIO çekirdek sürücüsünün yaşam döngüsü. Sürücü yalnızca normal modda ve program açıkken çalışır;
/// FIFA modunda ve program kapanınca durdurulur. Başlangıç türü her zaman "manual" yapılır ki
/// Windows açılışında kendiliğinden yüklenmesin.
/// FIFA modunda durdurma doğrulanamazsa <see cref="DriverStatus.FifaStopFailed"/> döner: kullanıcı yüksek sesle uyarılmalı.
/// </summary>
public sealed class PawnIoDriver(IServiceControl sc, TimeSpan? retryDelay = null)
{
    public const string ServiceName = "PawnIO";
    const int StopAttempts = 3;
    readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);

    public DriverStatus Prepare(bool fifaMode)
    {
        ServiceState? state;
        try { state = sc.Query(ServiceName); }
        catch (Exception) { return fifaMode ? DriverStatus.FifaStopFailed : DriverStatus.Failed; }
        if (state is null) return DriverStatus.NotInstalled;

        try { sc.SetManualStart(ServiceName); } catch (Exception) { /* başlangıç türü değişmese de devam */ }

        if (fifaMode) return StopVerified() ? DriverStatus.DisabledForFifa : DriverStatus.FifaStopFailed;

        try
        {
            if (state != ServiceState.Running) sc.Start(ServiceName);
            return DriverStatus.Ready;
        }
        catch (Exception) { return DriverStatus.Failed; }
    }

    /// <summary>Durdurur ve gerçekten durduğunu sorgulayarak doğrular; beklemedeyse tekrar dener.</summary>
    bool StopVerified()
    {
        for (var i = 0; i < StopAttempts; i++)
        {
            try
            {
                if (sc.Query(ServiceName) is null or ServiceState.Stopped) return true;
                sc.Stop(ServiceName);
                if (sc.Query(ServiceName) is null or ServiceState.Stopped) return true;
            }
            catch (Exception) { }
            if (i < StopAttempts - 1 && _retryDelay > TimeSpan.Zero) Thread.Sleep(_retryDelay);
        }
        return false;
    }

    public void Shutdown()
    {
        try { sc.Stop(ServiceName); }
        catch (Exception) { }
    }
}
