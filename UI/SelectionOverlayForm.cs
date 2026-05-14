using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AOUU.UI;

public sealed class SelectionOverlayForm : Form
{
    private const int HitZone = 8;
    private const int MinSelectionSize = 8;
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
    private readonly Button _cancelButton;
    private readonly Label _sizeLabel;
    private Point _dragStart;
    private Rectangle _dragStartSelection = Rectangle.Empty;
    private Rectangle _currentSelection = Rectangle.Empty;
    private DragMode _dragMode = DragMode.None;
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

        _sizeLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(210, 25, 25, 25),
            Padding = new Padding(10, 7, 10, 7),
            Visible = true
        };
        Controls.Add(_sizeLabel);

        _confirmButton = new Button
        {
            Width = 140,
            Height = 42,
            Text = "确认",
            Visible = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(235, 245, 245, 245)
        };
        _confirmButton.FlatAppearance.BorderColor = Color.FromArgb(35, 35, 35);
        _confirmButton.FlatAppearance.BorderSize = 1;
        _confirmButton.Click += (_, _) => ConfirmCurrentStage();
        Controls.Add(_confirmButton);

        _cancelButton = new Button
        {
            Width = 100,
            Height = 42,
            Text = "取消",
            Visible = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(235, 245, 245, 245)
        };
        _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(35, 35, 35);
        _cancelButton.FlatAppearance.BorderSize = 1;
        _cancelButton.Click += (_, _) => CancelSelection();
        Controls.Add(_cancelButton);

        MouseDown += SelectionOverlayForm_MouseDown;
        MouseMove += SelectionOverlayForm_MouseMove;
        MouseUp += SelectionOverlayForm_MouseUp;
        KeyDown += SelectionOverlayForm_KeyDown;
        Shown += SelectionOverlayForm_Shown;
        Resize += (_, _) => LayoutOverlayControls();
        FormClosed += (_, _) => _backgroundScreenshot.Dispose();
        UpdateStatusText();
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

        if (_stage == OverlayStage.Selecting && !_currentSelection.IsEmpty)
        {
            DrawSelectionRectangle(e.Graphics, _currentSelection, Color.LimeGreen);
            DrawResizeHandles(e.Graphics, _currentSelection);
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
        using var panelBrush = new SolidBrush(Color.FromArgb(55, 180, 0, 0));
        using var textBrush = new SolidBrush(Color.FromArgb(170, 255, 255, 255));
        using var titleFont = new Font(Font.FontFamily, 15, FontStyle.Bold);
        using var bodyFont = new Font(Font.FontFamily, 10, FontStyle.Regular);
        using var smallFont = new Font(Font.FontFamily, 9, FontStyle.Regular);

        string title;
        string detail;
        string hint;
        Size? promptSize = null;

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
            hint = "拖出区域后可移动或拖拽边角调整，Enter 确认，Esc 取消。";
            promptSize = step.PromptSize;
        }

        var panelWidth = promptSize?.Width ?? 640;
        var panelHeight = promptSize?.Height ?? 132;
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

    private static void DrawResizeHandles(Graphics graphics, Rectangle rectangle)
    {
        using var handleBrush = new SolidBrush(Color.White);
        using var handlePen = new Pen(Color.LimeGreen, 1);

        foreach (var handle in GetHandleRectangles(rectangle))
        {
            graphics.FillRectangle(handleBrush, handle);
            graphics.DrawRectangle(handlePen, handle);
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

    private void SelectionOverlayForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelSelection();
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            ConfirmCurrentStage();
        }
    }

    private void SelectionOverlayForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            CancelSelection();
            return;
        }

        if (_stage != OverlayStage.Selecting || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragStart = e.Location;
        _dragStartSelection = _currentSelection;
        _dragMode = GetDragMode(e.Location);
        if (_dragMode == DragMode.None)
        {
            _dragMode = DragMode.Draw;
            _currentSelection = new Rectangle(e.Location, Size.Empty);
            _dragStartSelection = _currentSelection;
        }

        Capture = true;
        Invalidate();
    }

    private void SelectionOverlayForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_stage != OverlayStage.Selecting)
        {
            return;
        }

        if (_dragMode == DragMode.None)
        {
            Cursor = GetCursor(GetDragMode(e.Location));
            return;
        }

        _currentSelection = _dragMode switch
        {
            DragMode.Draw => GetRectangle(_dragStart, e.Location),
            DragMode.Move => MoveRectangle(_dragStartSelection, e.Location.X - _dragStart.X, e.Location.Y - _dragStart.Y),
            _ => ResizeRectangle(_dragStartSelection, _dragStart, e.Location, _dragMode)
        };

        _currentSelection = ClampRectangle(_currentSelection, ClientRectangle);
        UpdateStatusText();
        Invalidate();
    }

    private void SelectionOverlayForm_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_stage != OverlayStage.Selecting || e.Button != MouseButtons.Left)
        {
            return;
        }

        Capture = false;
        _dragMode = DragMode.None;

        if (_currentSelection.Width < MinSelectionSize || _currentSelection.Height < MinSelectionSize)
        {
            _currentSelection = Rectangle.Empty;
        }

        UpdateStatusText();
        Invalidate();
    }

    private void ConfirmCurrentStage()
    {
        if (_stage == OverlayStage.Reviewing)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (_currentSelection.Width < MinSelectionSize || _currentSelection.Height < MinSelectionSize)
        {
            System.Media.SystemSounds.Exclamation.Play();
            return;
        }

        _selectedBoundsScreen.Add(new Rectangle(
            _screenBounds.X + _currentSelection.X,
            _screenBounds.Y + _currentSelection.Y,
            _currentSelection.Width,
            _currentSelection.Height));

        if (_selectedBoundsScreen.Count >= _steps.Count)
        {
            if (!EnterReviewStage())
            {
                DialogResult = DialogResult.OK;
                Close();
            }

            return;
        }

        _currentSelection = Rectangle.Empty;
        Cursor = Cursors.Cross;
        UpdateStatusText();
        Invalidate();
    }

    private void CancelSelection()
    {
        DialogResult = DialogResult.Cancel;
        Close();
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
        _currentSelection = Rectangle.Empty;
        _confirmButton.Text = "确认保存";
        UpdateStatusText();
        LayoutOverlayControls();
        Invalidate();
        return true;
    }

    private void LayoutOverlayControls()
    {
        _sizeLabel.Left = 16;
        _sizeLabel.Top = 16;

        var totalWidth = _confirmButton.Width + 12 + _cancelButton.Width;
        _confirmButton.Left = (ClientSize.Width - totalWidth) / 2;
        _confirmButton.Top = ClientSize.Height - _confirmButton.Height - 28;
        _cancelButton.Left = _confirmButton.Right + 12;
        _cancelButton.Top = _confirmButton.Top;
        _sizeLabel.BringToFront();
        _confirmButton.BringToFront();
        _cancelButton.BringToFront();
    }

    private void UpdateStatusText()
    {
        if (_stage == OverlayStage.Reviewing)
        {
            _sizeLabel.Text = "预览确认：Enter 或“确认保存”保存，Esc 或“取消”放弃。";
            LayoutOverlayControls();
            return;
        }

        if (_currentSelection.IsEmpty)
        {
            _sizeLabel.Text = "位置：未选择。拖动框选区域，Enter 确认，Esc 取消。";
        }
        else
        {
            var screenSelection = new Rectangle(
                _screenBounds.X + _currentSelection.X,
                _screenBounds.Y + _currentSelection.Y,
                _currentSelection.Width,
                _currentSelection.Height);
            _sizeLabel.Text = $"位置：X={screenSelection.X} Y={screenSelection.Y} 宽={screenSelection.Width} 高={screenSelection.Height}";
        }

        LayoutOverlayControls();
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

    private DragMode GetDragMode(Point point)
    {
        if (_currentSelection.IsEmpty)
        {
            return DragMode.None;
        }

        var left = Math.Abs(point.X - _currentSelection.Left) <= HitZone;
        var right = Math.Abs(point.X - _currentSelection.Right) <= HitZone;
        var top = Math.Abs(point.Y - _currentSelection.Top) <= HitZone;
        var bottom = Math.Abs(point.Y - _currentSelection.Bottom) <= HitZone;
        var withinHorizontal = point.X >= _currentSelection.Left - HitZone && point.X <= _currentSelection.Right + HitZone;
        var withinVertical = point.Y >= _currentSelection.Top - HitZone && point.Y <= _currentSelection.Bottom + HitZone;

        if (left && top)
        {
            return DragMode.ResizeTopLeft;
        }

        if (right && top)
        {
            return DragMode.ResizeTopRight;
        }

        if (left && bottom)
        {
            return DragMode.ResizeBottomLeft;
        }

        if (right && bottom)
        {
            return DragMode.ResizeBottomRight;
        }

        if (top && withinHorizontal)
        {
            return DragMode.ResizeTop;
        }

        if (bottom && withinHorizontal)
        {
            return DragMode.ResizeBottom;
        }

        if (left && withinVertical)
        {
            return DragMode.ResizeLeft;
        }

        if (right && withinVertical)
        {
            return DragMode.ResizeRight;
        }

        return _currentSelection.Contains(point) ? DragMode.Move : DragMode.None;
    }

    private static Cursor GetCursor(DragMode mode)
    {
        return mode switch
        {
            DragMode.Move => Cursors.SizeAll,
            DragMode.ResizeLeft or DragMode.ResizeRight => Cursors.SizeWE,
            DragMode.ResizeTop or DragMode.ResizeBottom => Cursors.SizeNS,
            DragMode.ResizeTopLeft or DragMode.ResizeBottomRight => Cursors.SizeNWSE,
            DragMode.ResizeTopRight or DragMode.ResizeBottomLeft => Cursors.SizeNESW,
            _ => Cursors.Cross
        };
    }

    private static Rectangle MoveRectangle(Rectangle rectangle, int deltaX, int deltaY)
    {
        rectangle.Offset(deltaX, deltaY);
        return rectangle;
    }

    private static Rectangle ResizeRectangle(Rectangle rectangle, Point start, Point current, DragMode mode)
    {
        var left = rectangle.Left;
        var top = rectangle.Top;
        var right = rectangle.Right;
        var bottom = rectangle.Bottom;
        var deltaX = current.X - start.X;
        var deltaY = current.Y - start.Y;

        if (mode is DragMode.ResizeLeft or DragMode.ResizeTopLeft or DragMode.ResizeBottomLeft)
        {
            left += deltaX;
        }

        if (mode is DragMode.ResizeRight or DragMode.ResizeTopRight or DragMode.ResizeBottomRight)
        {
            right += deltaX;
        }

        if (mode is DragMode.ResizeTop or DragMode.ResizeTopLeft or DragMode.ResizeTopRight)
        {
            top += deltaY;
        }

        if (mode is DragMode.ResizeBottom or DragMode.ResizeBottomLeft or DragMode.ResizeBottomRight)
        {
            bottom += deltaY;
        }

        if (right - left < MinSelectionSize)
        {
            if (mode is DragMode.ResizeLeft or DragMode.ResizeTopLeft or DragMode.ResizeBottomLeft)
            {
                left = right - MinSelectionSize;
            }
            else
            {
                right = left + MinSelectionSize;
            }
        }

        if (bottom - top < MinSelectionSize)
        {
            if (mode is DragMode.ResizeTop or DragMode.ResizeTopLeft or DragMode.ResizeTopRight)
            {
                top = bottom - MinSelectionSize;
            }
            else
            {
                bottom = top + MinSelectionSize;
            }
        }

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static Rectangle ClampRectangle(Rectangle rectangle, Rectangle bounds)
    {
        if (rectangle.IsEmpty)
        {
            return rectangle;
        }

        var width = Math.Min(rectangle.Width, bounds.Width);
        var height = Math.Min(rectangle.Height, bounds.Height);
        var x = Math.Clamp(rectangle.X, bounds.Left, bounds.Right - width);
        var y = Math.Clamp(rectangle.Y, bounds.Top, bounds.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    private static IEnumerable<Rectangle> GetHandleRectangles(Rectangle rectangle)
    {
        var centerX = rectangle.Left + rectangle.Width / 2;
        var centerY = rectangle.Top + rectangle.Height / 2;
        var points = new[]
        {
            new Point(rectangle.Left, rectangle.Top),
            new Point(centerX, rectangle.Top),
            new Point(rectangle.Right, rectangle.Top),
            new Point(rectangle.Right, centerY),
            new Point(rectangle.Right, rectangle.Bottom),
            new Point(centerX, rectangle.Bottom),
            new Point(rectangle.Left, rectangle.Bottom),
            new Point(rectangle.Left, centerY)
        };

        foreach (var point in points)
        {
            yield return new Rectangle(point.X - 4, point.Y - 4, 8, 8);
        }
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

    private enum DragMode
    {
        None,
        Draw,
        Move,
        ResizeLeft,
        ResizeRight,
        ResizeTop,
        ResizeBottom,
        ResizeTopLeft,
        ResizeTopRight,
        ResizeBottomLeft,
        ResizeBottomRight
    }
}

public sealed record SelectionStep(string Title, string Detail, Size? PromptSize = null);

public sealed record SelectionOverlayContext(
    Bitmap Snapshot,
    Rectangle ScreenBounds,
    IReadOnlyList<Rectangle> SelectedBoundsScreenList);

public sealed record SelectionReviewData(
    string Title,
    string Detail,
    IReadOnlyList<MarkerPoint> Markers);
