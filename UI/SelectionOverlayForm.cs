using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AOUU.UI;

public sealed class SelectionOverlayForm : Form
{
    private const int SwShow = 5;
    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpShowWindow = 0x0040;

    private readonly IReadOnlyList<SelectionStep> _steps;
    private readonly Rectangle _screenBounds;
    private readonly Bitmap _backgroundScreenshot;
    private readonly List<Rectangle> _selectedBoundsScreen = [];
    private readonly Func<SelectionOverlayContext, SelectionReviewData?>? _reviewFactory;
    private readonly Button _confirmButton;
    private Point? _dragStart;
    private Point _dragCurrent;
    private OverlayStage _stage = OverlayStage.Selecting;

    public SelectionOverlayForm(
        IEnumerable<SelectionStep> steps,
        Func<SelectionOverlayContext, SelectionReviewData?>? reviewFactory = null)
    {
        _steps = steps.ToList();
        if (_steps.Count == 0)
        {
            throw new ArgumentException("At least one selection step is required.", nameof(steps));
        }

        _reviewFactory = reviewFactory;

        var targetScreen = Screen.FromPoint(Cursor.Position);
        _screenBounds = targetScreen.Bounds;
        _backgroundScreenshot = new Bitmap(_screenBounds.Width, _screenBounds.Height);

        using (var graphics = Graphics.FromImage(_backgroundScreenshot))
        {
            graphics.CopyFromScreen(_screenBounds.Location, Point.Empty, _screenBounds.Size);
        }

        StartPosition = FormStartPosition.Manual;
        Bounds = _screenBounds;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;

        _confirmButton = new Button
        {
            Width = 140,
            Height = 42,
            Text = "确认保存",
            Visible = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(235, 245, 245, 245)
        };
        _confirmButton.FlatAppearance.BorderColor = Color.FromArgb(35, 35, 35);
        _confirmButton.FlatAppearance.BorderSize = 1;
        _confirmButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_confirmButton);

        MouseDown += SelectionOverlayForm_MouseDown;
        MouseMove += SelectionOverlayForm_MouseMove;
        MouseUp += SelectionOverlayForm_MouseUp;
        KeyDown += SelectionOverlayForm_KeyDown;
        Shown += SelectionOverlayForm_Shown;
        Resize += (_, _) => LayoutOverlayControls();
        FormClosed += (_, _) => _backgroundScreenshot.Dispose();
    }

    public Rectangle? SelectedBoundsScreen => _selectedBoundsScreen.FirstOrDefault();

    public IReadOnlyList<Rectangle> SelectedBoundsScreenList => _selectedBoundsScreen;

    public SelectionReviewData? ReviewData { get; private set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.DrawImageUnscaled(_backgroundScreenshot, Point.Empty);
        using var overlayBrush = new SolidBrush(Color.FromArgb(96, 0, 0, 0));
        e.Graphics.FillRectangle(overlayBrush, ClientRectangle);

        foreach (var rectangle in _selectedBoundsScreen.Select(ToClientRectangle))
        {
            DrawSelectionRectangle(e.Graphics, rectangle, Color.DodgerBlue);
        }

        if (_dragStart is not null && _stage == OverlayStage.Selecting)
        {
            var draggingRectangle = GetRectangle(_dragStart.Value, _dragCurrent);
            DrawSelectionRectangle(e.Graphics, draggingRectangle, Color.LimeGreen);
        }

        if (_stage == OverlayStage.Reviewing && ReviewData is not null)
        {
            foreach (var marker in ReviewData.Markers)
            {
                DrawMarker(e.Graphics, PointToClient(marker.ScreenPoint), marker.Label, marker.Color);
            }
        }

        DrawCenteredHint(e.Graphics);
    }

    private void DrawCenteredHint(Graphics graphics)
    {
        using var panelBrush = new SolidBrush(Color.FromArgb(220, 22, 22, 22));
        using var textBrush = new SolidBrush(Color.White);
        using var titleFont = new Font(Font.FontFamily, 15, FontStyle.Bold);
        using var bodyFont = new Font(Font.FontFamily, 10, FontStyle.Regular);
        using var smallFont = new Font(Font.FontFamily, 9, FontStyle.Regular);

        string title;
        string detail;
        string hint;

        if (_stage == OverlayStage.Reviewing && ReviewData is not null)
        {
            title = ReviewData.Title;
            detail = ReviewData.Detail;
            hint = "标记会一直保留在当前快照上，点击下方“确认保存”后才会退出。";
        }
        else
        {
            var currentStepIndex = Math.Min(_selectedBoundsScreen.Count, _steps.Count - 1);
            var step = _steps[currentStepIndex];
            title = $"第 {currentStepIndex + 1}/{_steps.Count} 步：{step.Title}";
            detail = step.Detail;
            hint = "按住左键拖拽框选，按 Esc 或右键取消。";
        }

        const int panelWidth = 640;
        const int panelHeight = 132;
        var tipRect = new Rectangle(
            (ClientSize.Width - panelWidth) / 2,
            (ClientSize.Height - panelHeight) / 2,
            panelWidth,
            panelHeight);

        graphics.FillRectangle(panelBrush, tipRect);

        var titleRect = new RectangleF(tipRect.X + 24, tipRect.Y + 18, tipRect.Width - 48, 28);
        var detailRect = new RectangleF(tipRect.X + 24, tipRect.Y + 52, tipRect.Width - 48, 30);
        var hintRect = new RectangleF(tipRect.X + 24, tipRect.Y + 88, tipRect.Width - 48, 26);

        graphics.DrawString(title, titleFont, textBrush, titleRect);
        graphics.DrawString(detail, bodyFont, textBrush, detailRect);
        graphics.DrawString(hint, smallFont, textBrush, hintRect);
    }

    private void DrawSelectionRectangle(Graphics graphics, Rectangle rectangle, Color borderColor)
    {
        graphics.DrawImage(_backgroundScreenshot, rectangle, rectangle, GraphicsUnit.Pixel);

        using var fillBrush = new SolidBrush(Color.FromArgb(72, borderColor));
        using var borderPen = new Pen(borderColor, 2);
        graphics.FillRectangle(fillBrush, rectangle);
        graphics.DrawRectangle(borderPen, rectangle);
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

    private void SelectionOverlayForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void SelectionOverlayForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        if (_stage != OverlayStage.Selecting || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragStart = e.Location;
        _dragCurrent = e.Location;
        Invalidate();
    }

    private void SelectionOverlayForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_stage != OverlayStage.Selecting || _dragStart is null)
        {
            return;
        }

        _dragCurrent = e.Location;
        Invalidate();
    }

    private void SelectionOverlayForm_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_stage != OverlayStage.Selecting || _dragStart is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragCurrent = e.Location;
        var clientRectangle = GetRectangle(_dragStart.Value, _dragCurrent);
        _dragStart = null;

        if (clientRectangle.Width < 5 || clientRectangle.Height < 5)
        {
            Invalidate();
            return;
        }

        _selectedBoundsScreen.Add(new Rectangle(
            _screenBounds.X + clientRectangle.X,
            _screenBounds.Y + clientRectangle.Y,
            clientRectangle.Width,
            clientRectangle.Height));

        if (_selectedBoundsScreen.Count >= _steps.Count)
        {
            if (!EnterReviewStage())
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
        }

        Invalidate();
    }

    private bool EnterReviewStage()
    {
        if (_reviewFactory is null)
        {
            return false;
        }

        using var snapshotCopy = new Bitmap(_backgroundScreenshot);
        var context = new SelectionOverlayContext(snapshotCopy, _screenBounds, _selectedBoundsScreen.ToArray());
        ReviewData = _reviewFactory(context);
        _stage = OverlayStage.Reviewing;
        Cursor = Cursors.Default;
        _confirmButton.Visible = true;
        LayoutOverlayControls();
        return true;
    }

    private void LayoutOverlayControls()
    {
        if (!_confirmButton.Visible)
        {
            return;
        }

        _confirmButton.Left = (ClientSize.Width - _confirmButton.Width) / 2;
        _confirmButton.Top = (ClientSize.Height / 2) + 92;
        _confirmButton.BringToFront();
    }

    private Rectangle ToClientRectangle(Rectangle screenRectangle)
    {
        return new Rectangle(
            screenRectangle.X - _screenBounds.X,
            screenRectangle.Y - _screenBounds.Y,
            screenRectangle.Width,
            screenRectangle.Height);
    }

    private static Rectangle GetRectangle(Point first, Point second)
    {
        return Rectangle.FromLTRB(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y));
    }

    private void SelectionOverlayForm_Shown(object? sender, EventArgs e)
    {
        ForceForeground();
    }

    private void ForceForeground()
    {
        ShowWindow(Handle, SwShow);
        SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowWindow);

        var foregroundWindow = GetForegroundWindow();
        var foregroundThreadId = foregroundWindow == IntPtr.Zero
            ? 0u
            : GetWindowThreadProcessId(foregroundWindow, out _);
        var currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(foregroundThreadId, currentThreadId, true);
        }

        BringWindowToTop(Handle);
        SetForegroundWindow(Handle);
        SetFocus(Handle);
        Activate();

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(foregroundThreadId, currentThreadId, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    private enum OverlayStage
    {
        Selecting,
        Reviewing
    }
}

public sealed record SelectionStep(string Title, string Detail);

public sealed record SelectionOverlayContext(
    Bitmap Snapshot,
    Rectangle ScreenBounds,
    IReadOnlyList<Rectangle> SelectedBoundsScreenList);

public sealed record SelectionReviewData(
    string Title,
    string Detail,
    IReadOnlyList<MarkerPoint> Markers);
