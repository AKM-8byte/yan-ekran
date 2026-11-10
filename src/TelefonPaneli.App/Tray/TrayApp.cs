using System.Drawing.Drawing2D;
using TelefonPaneli.Core.Security;
using TelefonPaneli.Windows.Driver;

namespace TelefonPaneli.App.Tray;

public sealed record TrayOptions(
    string Url,
    bool FifaMode,
    DriverStatus Driver,
    bool PresentMonFound,
    PairingService Pairing,
    Func<bool> FifaRunning,
    Action RestartInOtherMode);

/// <summary>Saatin yanındaki simge ve menüsü. Ayrı bir ana pencere yoktur.</summary>
public sealed class TrayApp : ApplicationContext
{
    readonly TrayOptions _o;
    readonly NotifyIcon _icon;
    readonly ToolStripMenuItem _codeItem;
    readonly System.Windows.Forms.Timer _fifaTimer;
    readonly SynchronizationContext _ui;
    QrForm? _qr;
    bool _fifaWarned;

    public TrayApp(TrayOptions o)
    {
        _o = o;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Telefon adresi: " + o.Url, null, (_, _) => ShowQr()) { Font = new Font(menu.Font, FontStyle.Bold) });
        _codeItem = new ToolStripMenuItem("Eşleştirme kodu: " + o.Pairing.CurrentCode, null, (_, _) => ShowQr());
        menu.Items.Add(_codeItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Mod: " + (o.FifaMode ? "FIFA (sensör sürücüsü kapalı)" : "Normal")) { Enabled = false });
        if (DriverNote(o.Driver) is { } note) menu.Items.Add(new ToolStripMenuItem(note) { Enabled = false });
        if (!o.PresentMonFound) menu.Items.Add(new ToolStripMenuItem("FPS için PresentMon bulunamadı") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem(o.FifaMode ? "Normal modda yeniden başlat" : "FIFA modunda yeniden başlat", null, (_, _) => o.RestartInOtherMode()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Eşleşmiş cihazları unut", null, (_, _) => ForgetDevices()));
        menu.Items.Add(new ToolStripMenuItem("Çıkış", null, (_, _) => ExitThread()));

        _icon = new NotifyIcon
        {
            Icon = DrawIcon(o.FifaMode),
            Text = o.FifaMode ? "Telefon Paneli (FIFA modu)" : "Telefon Paneli",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => ShowQr();

        o.Pairing.CodeChanged += OnCodeChanged;

        _fifaTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _fifaTimer.Tick += (_, _) => CheckFifa();
        if (!o.FifaMode) _fifaTimer.Start();

        if (o.Driver == DriverStatus.FifaStopFailed)
        {
            _icon.ShowBalloonTip(15000, "Sensör sürücüsü durdurulamadı!",
                "PawnIO hâlâ yüklü. FIFA'yı açma. Sürücüyü kullanan başka programları (FanControl, HWiNFO vb.) kapatıp FIFA modunda yeniden başlat ya da bilgisayarı yeniden başlat.",
                ToolTipIcon.Error);
        }
        else _icon.ShowBalloonTip(4000, "Telefon Paneli çalışıyor",
            "Telefonda aç: " + o.Url + "\nEşleştirme kodu: " + o.Pairing.CurrentCode, ToolTipIcon.Info);
    }

    static string? DriverNote(DriverStatus s) => s switch
    {
        DriverStatus.NotInstalled => "CPU sıcaklığı için PawnIO kurulu değil",
        DriverStatus.Failed => "Sensör sürücüsü başlatılamadı",
        DriverStatus.FifaStopFailed => "⚠ Sürücü durdurulamadı — FIFA'yı AÇMA",
        _ => null,
    };

    public void ShowQr()
    {
        if (_qr is { IsDisposed: false })
        {
            _qr.Activate();
            return;
        }
        _qr = new QrForm(_o.Url, _o.Pairing.CurrentCode);
        _qr.Show();
        _qr.Activate();
    }

    /// <summary>Başka bir iş parçacığından (ikinci örnek sinyali) çağrılabilir.</summary>
    public void ShowQrFromAnyThread() => _ui.Post(_ => ShowQr(), null);

    void OnCodeChanged(string code) => _ui.Post(_ =>
    {
        _codeItem.Text = "Eşleştirme kodu: " + code;
        _qr?.UpdateCode(code);
    }, null);

    void ForgetDevices()
    {
        var r = MessageBox.Show("Eşleşmiş tüm telefonlar unutulsun mu? Telefonlar yeniden kod girmek zorunda kalacak.",
            "Telefon Paneli", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r == DialogResult.Yes) _o.Pairing.ForgetAll();
    }

    void CheckFifa()
    {
        var running = _o.FifaRunning();
        if (running && !_fifaWarned)
        {
            _fifaWarned = true;
            _icon.ShowBalloonTip(8000, "FIFA açıldı",
                "Programı FIFA modunda yeniden başlat (menü → FIFA modunda yeniden başlat).", ToolTipIcon.Warning);
        }
        else if (!running) _fifaWarned = false;
    }

    protected override void ExitThreadCore()
    {
        _fifaTimer.Stop();
        _o.Pairing.CodeChanged -= OnCodeChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _qr?.Close();
        base.ExitThreadCore();
    }

    /// <summary>Basit bir simge çizer: koyu kare üzerinde üç çubuk; FIFA modunda turuncu.</summary>
    static Icon DrawIcon(bool fifa)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(22, 26, 32));
            g.FillRectangle(bg, 1, 1, 30, 30);
            using var bar = new SolidBrush(fifa ? Color.FromArgb(251, 146, 60) : Color.FromArgb(74, 222, 128));
            g.FillRectangle(bar, 6, 18, 5, 9);
            g.FillRectangle(bar, 14, 11, 5, 16);
            g.FillRectangle(bar, 22, 5, 5, 22);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
