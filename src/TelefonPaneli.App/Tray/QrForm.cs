using QRCoder;

namespace TelefonPaneli.App.Tray;

/// <summary>Telefonla okutulacak adres QR kodu ve eşleştirme kodu.</summary>
public sealed class QrForm : Form
{
    readonly Label _code;

    public QrForm(string url, string pairingCode)
    {
        Text = "YanEkran — telefonu bağla";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(380, 520);

        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);
        var pic = new PictureBox
        {
            Image = Image.FromStream(new MemoryStream(png)),
            SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(30, 16, 320, 320),
        };

        var info = new Label
        {
            Text = "Telefonun kamerasıyla okut veya Chrome'da aç:\n" + url,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10),
            Bounds = new Rectangle(10, 340, 360, 50),
        };
        var codeTitle = new Label
        {
            Text = "Eşleştirme kodu",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10),
            Bounds = new Rectangle(10, 395, 360, 24),
        };
        _code = new Label
        {
            Text = pairingCode,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 36, FontStyle.Bold),
            Bounds = new Rectangle(10, 420, 360, 70),
        };
        Controls.AddRange([pic, info, codeTitle, _code]);
    }

    public void UpdateCode(string code)
    {
        if (InvokeRequired) BeginInvoke(() => _code.Text = code);
        else _code.Text = code;
    }
}
