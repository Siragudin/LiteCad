using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;

namespace LiteCad.Tests;

internal static class TestLinearInputCommit
{
    public static void WireStatusBar(CadSession session, StatusBar statusBar, ITool tool)
    {
        statusBar.BindSession(session);
        statusBar.TryCommitLengthInput = input => LinearInputCommit.TryCommitLength(
            tool,
            input,
            session.DisplayUnitSettings.LinearUnit,
            statusBar.LineInputLabelMode);
        statusBar.TryCommitRectangleSizeInput = sizes => LinearInputCommit.TryCommitRectangleSize(
            tool,
            sizes.Width,
            sizes.Height,
            session.DisplayUnitSettings.LinearUnit,
            statusBar.DualFieldLabels);
    }
}
