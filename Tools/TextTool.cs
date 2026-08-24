using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Texts;
using LiteCad.UI;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LiteCad.Tools;

public sealed class TextTool : ToolBase
{
    private enum Phase
    {
        Idle,
        ArrowFirst,
        Editing
    }

    private readonly List<SnapPoint> _visibleSnaps = [];
    private readonly DispatcherTimer _caretTimer;
    private Phase _phase = Phase.Idle;
    private TextNoteKind _kind = TextNoteKind.Plain;
    private PointF _origin;
    private PointF? _arrowTip;
    private PointF _cursor;
    private string _draft = string.Empty;
    private bool _caretVisible = true;

    public TextTool()
    {
        _caretTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _caretTimer.Tick += (_, _) =>
        {
            _caretVisible = !_caretVisible;
            Context?.RequestRedraw();
        };
    }

    public override ToolId Id => ToolId.Text;

    public override bool CapturesTextInput => _phase == Phase.Editing;

    internal bool IsEditing => _phase == Phase.Editing;

    internal string DraftText => _draft;

    public override void OnActivated()
    {
        ResetState();
        SetIdleStatus();
    }

    public override void OnDeactivated()
    {
        ResetState();
        Context?.RequestRedraw();
    }

    public override void OnMouseDown(MouseButtonEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        SyncKind();
        if (e.ChangedButton == MouseButton.Right)
        {
            HandleRightClick();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var snapped = ResolveSnap(world);
        if (_phase == Phase.Editing)
        {
            CommitIfNeeded();
        }

        if (_kind == TextNoteKind.Plain)
        {
            BeginEditing(snapped, arrowTip: null);
            e.Handled = true;
            return;
        }

        if (_phase != Phase.ArrowFirst)
        {
            _arrowTip = snapped;
            _cursor = snapped;
            _phase = Phase.ArrowFirst;
            Context.SetStatus(Strings.Input_Text_SelectLanding);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        BeginEditing(snapped, _arrowTip);
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        SyncKind();
        _cursor = ResolveSnap(world);
        _visibleSnaps.Clear();
        _visibleSnaps.AddRange(
            Context.Session.SnapService.GetVisibleSnaps(
                Context.Session.Document,
                world,
                Context.SnapTolerance));
        RequestOverlayRedraw();
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            HandleRightClick();
            e.Handled = true;
            return;
        }

        if (_phase != Phase.Editing)
        {
            return;
        }

        if (key == Key.Enter)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                InsertNewLine();
            }
            else
            {
                CommitIfNeeded();
                SetIdleStatus();
            }

            e.Handled = true;
            return;
        }

        if (key == Key.Back)
        {
            if (_draft.Length > 0)
            {
                _draft = _draft[..^1];
                _caretVisible = true;
                Context.RequestRedraw();
            }

            e.Handled = true;
        }
    }

    public override void OnTextInput(TextCompositionEventArgs e)
    {
        if (Context is null || _phase != Phase.Editing || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (e.Text is "\r" or "\n" or "\r\n")
        {
            e.Handled = true;
            return;
        }

        _draft += e.Text;
        _caretVisible = true;
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null)
        {
            return;
        }

        SyncKind();
        var zoom = camera.Zoom;
        foreach (var snap in _visibleSnaps)
        {
            SnapRenderer.DrawSnapPoint(context, snap, zoom);
        }

        var previewColor = PreviewLineRenderer.GetAnnotationPreviewColor();
        if (_phase == Phase.ArrowFirst && _arrowTip is PointF tip)
        {
            TextAnnotationDrawing.DrawPreviewShaft(context, tip, _cursor, zoom, previewColor);
            return;
        }

        if (_phase == Phase.Editing)
        {
            TextAnnotationDrawing.DrawDraft(
                context,
                _kind,
                _origin,
                _arrowTip,
                _draft,
                Context.Session.TextToolOptions.TextSize,
                zoom,
                previewColor,
                camera,
                viewport,
                _caretVisible);
        }
    }

    internal void Type(string text)
    {
        if (_phase != Phase.Editing || string.IsNullOrEmpty(text))
        {
            return;
        }

        _draft += text;
        _caretVisible = true;
    }

    internal void InsertNewLine()
    {
        if (_phase != Phase.Editing)
        {
            return;
        }

        _draft += "\n";
        _caretVisible = true;
        Context?.RequestRedraw();
    }

    private void BeginEditing(PointF origin, PointF? arrowTip)
    {
        if (Context is null)
        {
            return;
        }

        _origin = origin;
        _arrowTip = arrowTip;
        _draft = string.Empty;
        _phase = Phase.Editing;
        _caretVisible = true;
        _caretTimer.Start();
        Context.SetStatus(Strings.Input_Text_Type);
        Context.RequestRedraw();
    }

    private void CommitIfNeeded()
    {
        if (Context is null || _phase != Phase.Editing)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_draft))
        {
            Context.RecordUndo();
            TextNoteService.Create(
                Context.Session.Document,
                _kind,
                _origin,
                _kind == TextNoteKind.Arrow ? _arrowTip : null,
                _draft,
                Context.Session.TextToolOptions.TextSize);
            Context.SetStatus(Strings.Status_TextCreated);
        }

        ResetState();
        Context.RequestRedraw();
    }

    private void HandleRightClick()
    {
        if (Context is null)
        {
            return;
        }

        var hadPending = _phase != Phase.Idle;
        ResetState();
        Context.SetStatus(hadPending ? Strings.Status_TextCancelled : Strings.Status_SelectionCleared);
        Context.RequestRedraw();
    }

    private void SetIdleStatus()
    {
        if (Context is null)
        {
            return;
        }

        SyncKind();
        Context.SetStatus(_kind == TextNoteKind.Arrow
            ? Strings.Input_Text_SelectArrow
            : Strings.Input_Text_SelectPoint);
    }

    private void SyncKind()
    {
        if (Context is null)
        {
            return;
        }

        var kind = Context.Session.TextToolOptions.Kind;
        if (kind == _kind)
        {
            return;
        }

        ResetState();
        _kind = kind;
    }

    private PointF ResolveSnap(PointF world)
    {
        if (Context is null)
        {
            return world;
        }

        return Context.Session.SnapService.ResolveDrawingSnap(
            Context.Session.Document,
            world,
            null,
            Context.SnapTolerance,
            orthoEnabled: false,
            includeOnEdge: true);
    }

    private void ResetState()
    {
        _caretTimer.Stop();
        _phase = Phase.Idle;
        _origin = PointF.Zero;
        _arrowTip = null;
        _draft = string.Empty;
        _caretVisible = true;
        _visibleSnaps.Clear();
        if (Context is not null)
        {
            _kind = Context.Session.TextToolOptions.Kind;
        }
    }
}
