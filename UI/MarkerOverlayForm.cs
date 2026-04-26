using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AOUU.UI;

public sealed class MarkerOverlayForm : Form
{
    private readonly IReadOnlyList<MarkerPoint> _points;
    private readonly System.Windows.Forms.Timer _closeTimer;

    public MarkerOverlayForm(IEnumerable<MarkerPoint> points, int durationMs)
    {
        _points = points.ToList();

        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;

        _closeTimer = new System.Windows.Forms.Timer();
        _closeTimer.Interval = durationMs;
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

        foreach (var point in _points)
        {
            var center = PointToClient(point.ScreenPoint);
            DrawMarker(e.Graphics, center, point.Label, point.Color);
        }
    }

    private static void DrawMarker(Graphics graphics, Point center, string label, Color color)
    {
        using var pen = new Pen(color, 3);
        using var brush = new SolidBrush(Color.FromArgb(220, 32, 32, 32));
        using var textBrush = new SolidBrush(Color.White);
        using var font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold);

        const int radius = 18;
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.DrawLine(pen, center.X - radius - 10, center.Y, center.X + radius + 10, center.Y);
        graphics.DrawLine(pen, center.X, center.Y - radius - 10, center.X, center.Y + radius + 10);

        var textSize = graphics.MeasureString(label, font);
        var textRect = new RectangleF(
            center.X + radius + 10,
            center.Y - (textSize.Height / 2),
            textSize.Width + 14,
            textSize.Height + 6);

        graphics.FillRectangle(brush, textRect);
        graphics.DrawString(label, font, textBrush, textRect.Left + 7, textRect.Top + 3);
    }
}

public sealed record MarkerPoint(Point ScreenPoint, string Label, Color Color);
