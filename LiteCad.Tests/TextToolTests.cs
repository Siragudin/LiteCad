using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Texts;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class TextToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void DefaultMode_IsPlain()
    {
        Assert.Equal(TextNoteKind.Plain, new TextToolOptions().Kind);
    }

    [Fact]
    public void PlainClick_TypeEnter_CreatesTextGoingRight()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(10, 20));
            Assert.True(harness.Tool.IsEditing);
            harness.Tool.Type("AB");
            harness.PressEnter();

            var note = Assert.Single(harness.Session.Document.Texts);
            Assert.Equal(TextNoteKind.Plain, note.Kind);
            Assert.Equal("AB", note.Text);
            Assert.True(MathUtils.ArePointsEqual(new PointF(10, 20), note.Origin, Tol));
            Assert.Null(note.ArrowTip);
            var layout = TextGeometry.CreateLayout(note, 1);
            Assert.True(layout.UnderlineEnd.X > note.Origin.X);
        });
    }

    [Fact]
    public void EnterOnEmptyDraft_DoesNotCreateText()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(0, 0));
            harness.PressEnter();
            Assert.Empty(harness.Session.Document.Texts);
        });
    }

    [Fact]
    public void AltEnter_InsertsNewLineThenEnterCommits()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(0, 0));
            harness.Tool.Type("A");
            harness.Tool.InsertNewLine();
            harness.Tool.Type("B");
            harness.PressEnter();

            var note = Assert.Single(harness.Session.Document.Texts);
            Assert.Equal("A\nB", note.Text);
        });
    }

    [Fact]
    public void ArrowMode_TwoClicksThenType_CreatesPointer()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.TextToolOptions.Kind = TextNoteKind.Arrow;
            harness.LeftClick(new PointF(0, 0));
            Assert.False(harness.Tool.IsEditing);
            harness.LeftClick(new PointF(40, 25));
            Assert.True(harness.Tool.IsEditing);
            harness.Tool.Type("Hi");
            harness.PressEnter();

            var note = Assert.Single(harness.Session.Document.Texts);
            Assert.Equal(TextNoteKind.Arrow, note.Kind);
            Assert.Equal("Hi", note.Text);
            Assert.True(MathUtils.ArePointsEqual(new PointF(40, 25), note.Origin, Tol));
            Assert.True(note.ArrowTip.HasValue);
            Assert.True(MathUtils.ArePointsEqual(new PointF(0, 0), note.ArrowTip.Value, Tol));
            var layout = TextGeometry.CreateLayout(note, 1);
            Assert.Equal(note.Origin.Y, layout.UnderlineEnd.Y, 3);
            Assert.True(layout.UnderlineEnd.X > note.Origin.X);
        });
    }

    [Fact]
    public void SerializeRoundTrip_PreservesTextNotes()
    {
        var document = new CadDocument();
        TextNoteService.Create(document, TextNoteKind.Plain, new PointF(5, 6), null, "Hello", 12);
        TextNoteService.Create(document, TextNoteKind.Arrow, new PointF(20, 0), new PointF(0, 10), "Ptr", 14);

        var json = ProjectDocumentSerializer.Serialize(document, LinearDisplayUnit.Millimeters);
        var loaded = new CadDocument();
        ProjectDocumentSerializer.Apply(loaded, ProjectDocumentSerializer.Deserialize(json));

        Assert.Equal(2, loaded.Texts.Count);
        Assert.Equal("Hello", loaded.Texts[0].Text);
        Assert.Equal(TextNoteKind.Arrow, loaded.Texts[1].Kind);
        Assert.Equal("Ptr", loaded.Texts[1].Text);
        Assert.True(loaded.Texts[1].ArrowTip.HasValue);
    }

    [Fact]
    public void Undo_RestoresRemovedText()
    {
        var session = new CadSession();
        session.History.Record(session.Document);
        TextNoteService.Create(session.Document, TextNoteKind.Plain, PointF.Zero, null, "X", 12);
        Assert.Single(session.Document.Texts);
        session.History.Undo(session.Document, Tol);
        Assert.Empty(session.Document.Texts);
        session.History.Redo(session.Document, Tol);
        Assert.Single(session.Document.Texts);
    }

    [Fact]
    public void TrailingNewLine_MovesCaretToNextLine()
    {
        RunSta(() =>
        {
            var single = TextGeometry.MeasureScreen("A", 12);
            var withBreak = TextGeometry.MeasureScreen("A\n", 12);
            Assert.True(withBreak.Height > single.Height + 1);

            var caret = TextGeometry.GetCaretScreenOffset("A\n", 12);
            Assert.True(caret.Y >= single.Height - 1);
            Assert.Equal(0, caret.X, 3);
        });
    }

    [Fact]
    public void Move_TranslatesPlainTextAndArrowPointer()
    {
        var document = new CadDocument();
        var plain = TextNoteService.Create(document, TextNoteKind.Plain, new PointF(10, 20), null, "Hi", 12);
        var pointer = TextNoteService.Create(
            document,
            TextNoteKind.Arrow,
            new PointF(40, 25),
            new PointF(0, 0),
            "Ptr",
            12);
        var selection = new Selection();
        selection.SelectedTextIds.Add(plain.Id);
        selection.SelectedTextIds.Add(pointer.Id);

        Assert.True(MoveOperations.CanMove(selection));
        Assert.False(MoveOperations.UsesVertexMove(selection));

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        MoveOperations.ExecuteObjectMove(document, selection, snapshot, new PointF(5, -3));

        Assert.True(MathUtils.ArePointsEqual(plain.Origin, new PointF(15, 17), Tol));
        Assert.True(MathUtils.ArePointsEqual(pointer.Origin, new PointF(45, 22), Tol));
        Assert.True(pointer.ArrowTip.HasValue);
        Assert.True(MathUtils.ArePointsEqual(pointer.ArrowTip.Value, new PointF(5, -3), Tol));
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static TextToolHarness CreateHarness()
        => new();

    private sealed class TextToolHarness : IDisposable
    {
        public TextToolHarness()
        {
            Session = new CadSession();
            Tool = new TextTool();
            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document),
                activateSelectionTool: () => { });
            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public TextTool Tool { get; }

        public void LeftClick(PointF world)
            => Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), world);

        public void PressEnter()
        {
            var window = WpfTestUtilities.CreateHiddenWindow();
            try
            {
                Tool.OnKeyDown(WpfTestUtilities.CreateKeyDown(window, Key.Enter));
            }
            finally
            {
                window.Close();
            }
        }

        public void Dispose()
        {
        }
    }

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent
        };
}
