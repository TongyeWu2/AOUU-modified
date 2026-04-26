using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsApp1.UI;

public sealed class RecognitionResultOverlayForm : Form
{
    private readonly string _title;
    private readonly string _message;
    private readonly Color _accentColor;
    private readonly System.Windows.Forms.Timer _closeTimer;

    public RecognitionResultOverlayForm(string title, string message, Color accentColor, int durationMs)
    {
        _title = title;
        _message = message;
        _accentColor = accentColor;

        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;

        _closeTimer = new System.Windows.Forms.Timer();
        _closeTimer.Interval = Math.Max(600, durationMs);
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _closeTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _closeTimer.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var panelWidth = Math.Min(560, Math.Max(360, Width / 3));
        var panelHeight = 160;
        var panelRect = new Rectangle(
            (Width - panelWidth) / 2,
            Math.Max(60, Height / 7),
            panelWidth,
            panelHeight);

        using var backgroundBrush = new SolidBrush(Color.FromArgb(228, 18, 18, 18));
        using var borderPen = new Pen(_accentColor, 3);
        using var accentBrush = new SolidBrush(_accentColor);
        using var titleFont = new Font("Microsoft YaHei UI", 17, FontStyle.Bold);
        using var messageFont = new Font("Microsoft YaHei UI", 10, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.White);
        using var subtleBrush = new SolidBrush(Color.FromArgb(245, 235, 235, 235));

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillRectangle(backgroundBrush, panelRect);
        e.Graphics.DrawRectangle(borderPen, panelRect);

        var accentRect = new Rectangle(panelRect.X, panelRect.Y, 10, panelRect.Height);
        e.Graphics.FillRectangle(accentBrush, accentRect);

        var titleRect = new RectangleF(panelRect.X + 28, panelRect.Y + 24, panelRect.Width - 56, 36);
        var messageRect = new RectangleF(panelRect.X + 28, panelRect.Y + 72, panelRect.Width - 56, panelRect.Height - 92);

        e.Graphics.DrawString(_title, titleFont, textBrush, titleRect);
        e.Graphics.DrawString(_message, messageFont, subtleBrush, messageRect);
    }
}
