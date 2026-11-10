namespace TelefonPaneli.App;

/// <summary>Komut satırı: --fifa (FIFA modu), --after &lt;pid&gt; (yeniden başlatmada eski sürecin kapanmasını bekle).</summary>
public sealed record AppArgs(bool Fifa, int? AfterPid)
{
    public static AppArgs Parse(string[] args)
    {
        var fifa = false;
        int? after = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--fifa", StringComparison.OrdinalIgnoreCase)) fifa = true;
            else if (args[i].Equals("--after", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var pid))
            {
                after = pid;
                i++;
            }
        }
        return new AppArgs(fifa, after);
    }

    public static string RestartArguments(bool fifa, int currentPid) => (fifa ? "--fifa " : "") + "--after " + currentPid;
}
