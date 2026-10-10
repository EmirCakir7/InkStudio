using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkStudio.Controls;

public enum ActiveTool
{
    BallpointPen,
    Pencil,
    Highlighter,
    Eraser,
    LassoSelect
}

public class PenTelemetryEventArgs : EventArgs
{
    public bool IsPenPresent { get; set; }
    public float Pressure { get; set; }
    public double TiltX { get; set; }
    public double TiltY { get; set; }
    public bool IsInverted { get; set; }
    public bool IsBarrelDown { get; set; }
    public string DeviceName { get; set; } = string.Empty;
}

public class MppInkCanvas : InkCanvas
{
    public event EventHandler<PenTelemetryEventArgs>? PenTelemetryChanged;

    public ActiveTool CurrentTool { get; set; } = ActiveTool.BallpointPen;
    public bool TouchInkingEnabled { get; set; } = false; // Default: Finger = Pan/Zoom, Pen = Auto Ink

    public bool IsStylusInContact => _isStylusInContact;

    private bool _isStylusInContact = false;
    private readonly System.Collections.Generic.HashSet<int> _activeTouchIds = new();
    private bool _isMultiTouchGesture = false;
    private DateTime _lastStylusActiveTime = DateTime.MinValue;

    public bool IsStylusEngaged()
    {
        if (_isStylusInContact) return true;
        if ((DateTime.UtcNow - _lastStylusActiveTime).TotalMilliseconds < 350) return true;
        return false;
    }

    private DrawingAttributes _penAttributes = new()
    {
        Color = Colors.Black,
        Width = 6.0,
        Height = 6.0,
        FitToCurve = true,
        IgnorePressure = false,
        StylusTip = StylusTip.Ellipse
    };

    private DrawingAttributes _pencilAttributes = new()
    {
        Color = Color.FromArgb(210, 45, 45, 45),
        Width = 4.5,
        Height = 4.5,
        FitToCurve = true,
        IgnorePressure = false,
        StylusTip = StylusTip.Ellipse
    };

    private DrawingAttributes _highlighterAttributes = new()
    {
        Color = Color.FromArgb(120, 255, 235, 59),
        Width = 28,
        Height = 28,
        IsHighlighter = true,
        FitToCurve = false,
        IgnorePressure = true,
        StylusTip = StylusTip.Rectangle
    };

    public MppInkCanvas()
    {
        DefaultDrawingAttributes = _penAttributes;

        // Default mode: Inking mode matching current tool
        EditingMode = InkCanvasEditingMode.Ink;
        EditingModeInverted = InkCanvasEditingMode.EraseByStroke;

        IsHitTestVisible = true;
        Focusable = true;
    }

    public void SetPenColor(Color color)
    {
        _penAttributes.Color = color;
        _pencilAttributes.Color = Color.FromArgb(210, color.R, color.G, color.B);
        UpdateCurrentDrawingAttributes();
    }

    public void SetPenWidth(double width)
    {
        _penAttributes.Width = width * 1.5; // Amplified base thickness for high dynamic range pressure
        _penAttributes.Height = width * 1.5;
        _pencilAttributes.Width = Math.Max(2.0, width * 1.2);
        _pencilAttributes.Height = Math.Max(2.0, width * 1.2);
        _highlighterAttributes.Width = Math.Max(16, width * 4.5);
        _highlighterAttributes.Height = Math.Max(16, width * 4.5);
        UpdateCurrentDrawingAttributes();
    }

    public void SetTool(ActiveTool tool)
    {
        CurrentTool = tool;
        UpdateCurrentDrawingAttributes();

        if (tool == ActiveTool.LassoSelect)
        {
            EditingMode = InkCanvasEditingMode.Select;
        }
        else if (tool == ActiveTool.Eraser)
        {
            EditingMode = InkCanvasEditingMode.EraseByStroke;
            Select(new StrokeCollection());
        }
        else
        {
            EditingMode = GetActiveToolEditingMode();
            Select(new StrokeCollection());
        }
    }

    private void UpdateCurrentDrawingAttributes()
    {
        switch (CurrentTool)
        {
            case ActiveTool.BallpointPen:
                DefaultDrawingAttributes = _penAttributes;
                break;
            case ActiveTool.Pencil:
                DefaultDrawingAttributes = _pencilAttributes;
                break;
            case ActiveTool.Highlighter:
                DefaultDrawingAttributes = _highlighterAttributes;
                break;
        }
    }

    private InkCanvasEditingMode GetActiveToolEditingMode()
    {
        return CurrentTool switch
        {
            ActiveTool.Eraser => InkCanvasEditingMode.EraseByStroke,
            ActiveTool.LassoSelect => InkCanvasEditingMode.Select,
            _ => InkCanvasEditingMode.Ink
        };
    }

    // ========================================================
    // TOUCH & MULTI-TOUCH DETECTION (1 FINGER = INK, 2 FINGERS = ZOOM/PAN)
    // ========================================================

    protected override void OnPreviewTouchDown(TouchEventArgs e)
    {
        // Palm rejection: If stylus is touching or recently active, completely ignore palm touch
        if (IsStylusEngaged())
        {
            e.Handled = true;
            return;
        }

        _activeTouchIds.Add(e.TouchDevice.Id);

        if (_activeTouchIds.Count >= 2 || _isMultiTouchGesture)
        {
            _isMultiTouchGesture = true;

            // Switch to None so parent manipulation container pans and zooms
            EditingMode = InkCanvasEditingMode.None;
            ReleaseStylusCapture();
            ReleaseAllTouchCaptures();
        }
        else if (_activeTouchIds.Count == 1)
        {
            if (CurrentTool == ActiveTool.LassoSelect)
            {
                EditingMode = InkCanvasEditingMode.Select;
            }
            else if (TouchInkingEnabled)
            {
                UpdateCurrentDrawingAttributes();
                EditingMode = GetActiveToolEditingMode();
            }
            else
            {
                // When TouchInkingEnabled is false, single finger is reserved for panning/scrolling
                EditingMode = InkCanvasEditingMode.None;
            }
        }

        base.OnPreviewTouchDown(e);
    }

    protected override void OnPreviewTouchMove(TouchEventArgs e)
    {
        // Palm rejection
        if (IsStylusEngaged())
        {
            e.Handled = true;
            return;
        }
        base.OnPreviewTouchMove(e);
    }

    protected override void OnPreviewTouchUp(TouchEventArgs e)
    {
        if (IsStylusEngaged())
        {
            e.Handled = true;
            return;
        }

        _activeTouchIds.Remove(e.TouchDevice.Id);
        if (_activeTouchIds.Count == 0)
        {
            _isMultiTouchGesture = false;
            // Restore active tool mode once finger interaction ends
            if (CurrentTool == ActiveTool.LassoSelect)
            {
                EditingMode = InkCanvasEditingMode.Select;
            }
            else
            {
                EditingMode = GetActiveToolEditingMode();
            }
        }
        base.OnPreviewTouchUp(e);
    }

    // ========================================================
    // PEN / STYLUS DETECTION (MPP 2.0)
    // ========================================================

    protected override void OnPreviewStylusDown(StylusDownEventArgs e)
    {
        // 1. IF FINGER TOUCH (promoted to Stylus by WPF):
        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch)
        {
            // Palm rejection:
            if (IsStylusEngaged())
            {
                e.Handled = true;
                return;
            }

            _activeTouchIds.Add(e.StylusDevice.Id);

            if (_activeTouchIds.Count >= 2 || _isMultiTouchGesture)
            {
                _isMultiTouchGesture = true;
                EditingMode = InkCanvasEditingMode.None;
                ReleaseStylusCapture();
                ReleaseAllTouchCaptures();
                return;
            }

            if (!TouchInkingEnabled && CurrentTool != ActiveTool.LassoSelect)
            {
                EditingMode = InkCanvasEditingMode.None;
                return;
            }
        }

        // 2. IF MPP 2.0 STYLUS PEN:
        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus)
        {
            _isStylusInContact = true;
            _lastStylusActiveTime = DateTime.UtcNow;
            _isMultiTouchGesture = false; // Pen touch guarantees active drawing
        }

        if (e.StylusDevice.Inverted)
        {
            // Tail eraser
            EditingMode = InkCanvasEditingMode.EraseByStroke;
        }
        else if (IsBarrelButtonPressed(e.StylusDevice) || CurrentTool == ActiveTool.LassoSelect)
        {
            // Side barrel button or Lasso Tool selected:
            EditingMode = InkCanvasEditingMode.Select;
        }
        else
        {
            // Automatic instant inking when pen touches canvas
            UpdateCurrentDrawingAttributes();
            EditingMode = GetActiveToolEditingMode();
        }

        ReportTelemetry(e.StylusDevice);
        base.OnPreviewStylusDown(e);
    }

    protected override void OnPreviewStylusMove(StylusEventArgs e)
    {
        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch)
        {
            if (IsStylusEngaged())
            {
                e.Handled = true;
                return;
            }
        }
        else if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus)
        {
            _lastStylusActiveTime = DateTime.UtcNow;
            ReportTelemetry(e.StylusDevice);
        }

        base.OnPreviewStylusMove(e);
    }

    protected override void OnPreviewStylusUp(StylusEventArgs e)
    {
        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch)
        {
            _activeTouchIds.Remove(e.StylusDevice.Id);
            if (_activeTouchIds.Count == 0)
            {
                _isMultiTouchGesture = false;
                EditingMode = GetActiveToolEditingMode();
            }
        }
        else if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus)
        {
            _isStylusInContact = false;
            _lastStylusActiveTime = DateTime.UtcNow;
            ReportTelemetry(e.StylusDevice);
        }

        // Call base to finalize and save the stroke or lasso selection into Strokes collection
        base.OnPreviewStylusUp(e);
    }

    protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e)
    {
        // Stroke collected successfully: Base call preserves and fires stroke collection events
        base.OnStrokeCollected(e);
        // Note: Strokes are NEVER removed here.
    }

    protected override void OnStylusButtonDown(StylusButtonEventArgs e)
    {
        if (e.StylusButton.Guid == StylusPointProperties.BarrelButton.Id)
        {
            EditingMode = InkCanvasEditingMode.Select;
        }
        base.OnStylusButtonDown(e);
    }

    protected override void OnStylusButtonUp(StylusButtonEventArgs e)
    {
        if (CurrentTool == ActiveTool.LassoSelect)
        {
            EditingMode = InkCanvasEditingMode.Select;
        }
        else
        {
            EditingMode = GetActiveToolEditingMode();
        }
        base.OnStylusButtonUp(e);
    }

    // ========================================================
    // TELEMETRY
    // ========================================================

    private void ReportTelemetry(StylusDevice stylus)
    {
        if (PenTelemetryChanged == null) return;

        var points = stylus.GetStylusPoints(this);
        float pressure = 0f;
        double tiltX = 0;
        double tiltY = 0;

        if (points.Count > 0)
        {
            var pt = points[^1];
            pressure = pt.PressureFactor;

            if (pt.HasProperty(StylusPointProperties.XTiltOrientation))
            {
                tiltX = pt.GetPropertyValue(StylusPointProperties.XTiltOrientation);
            }
            if (pt.HasProperty(StylusPointProperties.YTiltOrientation))
            {
                tiltY = pt.GetPropertyValue(StylusPointProperties.YTiltOrientation);
            }
        }

        bool isBarrel = IsBarrelButtonPressed(stylus);

        PenTelemetryChanged?.Invoke(this, new PenTelemetryEventArgs
        {
            IsPenPresent = true,
            Pressure = pressure,
            TiltX = tiltX,
            TiltY = tiltY,
            IsInverted = stylus.Inverted,
            IsBarrelDown = isBarrel,
            DeviceName = stylus.TabletDevice?.Name ?? "MPP 2.0 Kalem"
        });
    }

    private static bool IsBarrelButtonPressed(StylusDevice stylus)
    {
        foreach (StylusButton btn in stylus.StylusButtons)
        {
            if (btn.Guid == StylusPointProperties.BarrelButton.Id && btn.StylusButtonState == StylusButtonState.Down)
            {
                return true;
            }
        }
        return false;
    }
}
