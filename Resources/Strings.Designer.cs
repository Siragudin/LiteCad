using System.Globalization;
using System.Resources;

namespace LiteCad.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("LiteCad.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key)
        => ResourceManager.GetString(key, LocalizationManager.Instance.CurrentCulture)
           ?? ResourceManager.GetString(key, AppLanguage.EnglishCulture)
           ?? key;

    public static string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    public static string Tool_Selection => Get("Tool.Selection");
    public static string Tool_Select_Short => Get("Tool.Select.Short");
    public static string Tool_Hand => Get("Tool.Hand");
    public static string Tool_Line => Get("Tool.Line");
    public static string Tool_Arc => Get("Tool.Arc");
    public static string Tool_Arc_Short => Get("Tool.Arc.Short");
    public static string Tool_Rectangle => Get("Tool.Rectangle");
    public static string Tool_Rectangle_Short => Get("Tool.Rectangle.Short");
    public static string Tool_Circle => Get("Tool.Circle");
    public static string Tool_Circle_Short => Get("Tool.Circle.Short");
    public static string Tool_Sector => Get("Tool.Sector");
    public static string Tool_Sector_Short => Get("Tool.Sector.Short");
    public static string Tool_Axis => Get("Tool.Axis");
    public static string Tool_Wall => Get("Tool.Wall");
    public static string Tool_Move => Get("Tool.Move");
    public static string Tool_Rotate => Get("Tool.Rotate");
    public static string Tool_Stretch => Get("Tool.Stretch");
    public static string Tool_Eraser => Get("Tool.Eraser");
    public static string Tool_Eraser_Short => Get("Tool.Eraser.Short");
    public static string Tool_Copy => Get("Tool.Copy");
    public static string Tool_PolygonEdit => Get("Tool.PolygonEdit");
    public static string Tool_Edit_Short => Get("Tool.Edit.Short");

    public static string Menu_File => Get("Menu.File");
    public static string Menu_New => Get("Menu.New");
    public static string Menu_Open => Get("Menu.Open");
    public static string Menu_Save => Get("Menu.Save");
    public static string Menu_Exit => Get("Menu.Exit");
    public static string Menu_Edit => Get("Menu.Edit");
    public static string Menu_Undo => Get("Menu.Undo");
    public static string Menu_Redo => Get("Menu.Redo");
    public static string Menu_Cut => Get("Menu.Cut");
    public static string Menu_Copy => Get("Menu.Copy");
    public static string Menu_Paste => Get("Menu.Paste");
    public static string Menu_Delete => Get("Menu.Delete");
    public static string Menu_View => Get("Menu.View");
    public static string Menu_ZoomIn => Get("Menu.ZoomIn");
    public static string Menu_ZoomOut => Get("Menu.ZoomOut");
    public static string Menu_ZoomExtents => Get("Menu.ZoomExtents");
    public static string Menu_Grid => Get("Menu.Grid");
    public static string Menu_Tools => Get("Menu.Tools");
    public static string Menu_Layers => Get("Menu.Layers");
    public static string Menu_Help => Get("Menu.Help");
    public static string Menu_About => Get("Menu.About");

    public static string Menu_Header_File => Get("Menu.Header.File");
    public static string Menu_Header_New => Get("Menu.Header.New");
    public static string Menu_Header_Open => Get("Menu.Header.Open");
    public static string Menu_Header_Save => Get("Menu.Header.Save");
    public static string Menu_Header_Exit => Get("Menu.Header.Exit");
    public static string Menu_Header_Edit => Get("Menu.Header.Edit");
    public static string Menu_Header_Undo => Get("Menu.Header.Undo");
    public static string Menu_Header_Redo => Get("Menu.Header.Redo");
    public static string Menu_Header_Cut => Get("Menu.Header.Cut");
    public static string Menu_Header_Copy => Get("Menu.Header.Copy");
    public static string Menu_Header_Paste => Get("Menu.Header.Paste");
    public static string Menu_Header_Delete => Get("Menu.Header.Delete");
    public static string Menu_Header_View => Get("Menu.Header.View");
    public static string Menu_Header_ZoomIn => Get("Menu.Header.ZoomIn");
    public static string Menu_Header_ZoomOut => Get("Menu.Header.ZoomOut");
    public static string Menu_Header_ZoomExtents => Get("Menu.Header.ZoomExtents");
    public static string Menu_Header_Grid => Get("Menu.Header.Grid");
    public static string Menu_Header_Tools => Get("Menu.Header.Tools");
    public static string Menu_Header_Selection => Get("Menu.Header.Selection");
    public static string Menu_Header_Hand => Get("Menu.Header.Hand");
    public static string Menu_Header_Line => Get("Menu.Header.Line");
    public static string Menu_Header_Arc => Get("Menu.Header.Arc");
    public static string Menu_Header_Axis => Get("Menu.Header.Axis");
    public static string Menu_Header_Wall => Get("Menu.Header.Wall");
    public static string Menu_Header_Rectangle => Get("Menu.Header.Rectangle");
    public static string Menu_Header_Circle => Get("Menu.Header.Circle");
    public static string Menu_Header_Sector => Get("Menu.Header.Sector");
    public static string Menu_Header_Move => Get("Menu.Header.Move");
    public static string Menu_Header_Rotate => Get("Menu.Header.Rotate");
    public static string Menu_Header_CopyTool => Get("Menu.Header.CopyTool");
    public static string Menu_Header_PolygonEdit => Get("Menu.Header.PolygonEdit");
    public static string Menu_Header_Layers => Get("Menu.Header.Layers");
    public static string Menu_Header_Help => Get("Menu.Header.Help");
    public static string Menu_Header_About => Get("Menu.Header.About");

    public static string Label_AppTitle => Get("Label.AppTitle");
    public static string Label_Properties => Get("Label.Properties");
    public static string Label_Tool => Get("Label.Tool");
    public static string Label_Selection => Get("Label.Selection");
    public static string Label_None => Get("Label.None");
    public static string Label_LineParameters => Get("Label.LineParameters");
    public static string Label_MoveParameters => Get("Label.MoveParameters");
    public static string Label_Color => Get("Label.Color");
    public static string Label_Thickness => Get("Label.Thickness");
    public static string Label_Type => Get("Label.Type");
    public static string Label_OrthoHorizontalVertical => Get("Label.OrthoHorizontalVertical");
    public static string Label_Ortho => Get("Label.Ortho");
    public static string Label_NoParameters => Get("Label.NoParameters");
    public static string Label_Length => Get("Label.Length");
    public static string Label_Width => Get("Label.Width");
    public static string Label_Height => Get("Label.Height");
    public static string Label_X => Get("Label.X");
    public static string Label_Y => Get("Label.Y");
    public static string Label_Distance => Get("Label.Distance");
    public static string Label_Radius => Get("Label.Radius");
    public static string Label_Angle => Get("Label.Angle");
    public static string Label_EmptyValue => Get("Label.EmptyValue");
    public static string Label_Color_Black => Get("Label.Color.Black");
    public static string Label_Color_Gray => Get("Label.Color.Gray");
    public static string Label_Color_Blue => Get("Label.Color.Blue");
    public static string Label_Color_Red => Get("Label.Color.Red");
    public static string Label_Color_Green => Get("Label.Color.Green");
    public static string Label_LineType_Solid => Get("Label.LineType.Solid");
    public static string Label_LineType_Dashed => Get("Label.LineType.Dashed");
    public static string Label_LineType_Dotted => Get("Label.LineType.Dotted");
    public static string Label_PolygonType_Face => Get("Label.PolygonType.Face");
    public static string Label_PolygonType_Wall => Get("Label.PolygonType.Wall");
    public static string Label_PolygonType_Room => Get("Label.PolygonType.Room");
    public static string Label_PolygonType_Axis => Get("Label.PolygonType.Axis");

    public static string Status_Ready => Get("Status.Ready");
    public static string Status_NewDocument => Get("Status.NewDocument");
    public static string Status_ToolActive => Get("Status.ToolActive");
    public static string Status_CopiedToClipboard => Get("Status.CopiedToClipboard");
    public static string Status_Pasted => Get("Status.Pasted");
    public static string Status_CutToClipboard => Get("Status.CutToClipboard");
    public static string Status_Deleted => Get("Status.Deleted");
    public static string Status_Undo => Get("Status.Undo");
    public static string Status_Redo => Get("Status.Redo");
    public static string Status_SelectionCleared => Get("Status.SelectionCleared");
    public static string Status_SelectionUpdated => Get("Status.SelectionUpdated");
    public static string Status_VertexSelected => Get("Status.VertexSelected");
    public static string Status_EdgeSelected => Get("Status.EdgeSelected");
    public static string Status_PolygonSelected => Get("Status.PolygonSelected");
    public static string Status_MultipleObjectsSelected => Get("Status.MultipleObjectsSelected");
    public static string Status_WindowSelection => Get("Status.WindowSelection");
    public static string Status_CrossingSelection => Get("Status.CrossingSelection");
    public static string Status_LineCancelled => Get("Status.LineCancelled");
    public static string Status_ArcCancelled => Get("Status.ArcCancelled");
    public static string Status_RectangleCancelled => Get("Status.RectangleCancelled");
    public static string Status_CircleCancelled => Get("Status.CircleCancelled");
    public static string Status_SectorCancelled => Get("Status.SectorCancelled");
    public static string Status_MoveCancelled => Get("Status.MoveCancelled");
    public static string Status_MoveCompleted => Get("Status.MoveCompleted");
    public static string Status_RotateCancelled => Get("Status.RotateCancelled");
    public static string Status_RotateCompleted => Get("Status.RotateCompleted");
    public static string Status_CopyCancelled => Get("Status.CopyCancelled");
    public static string Status_CopyCompleted => Get("Status.CopyCompleted");
    public static string Status_StretchCancelled => Get("Status.StretchCancelled");
    public static string Status_StretchCompleted => Get("Status.StretchCompleted");

    public static string Input_Line_SelectEndPoint => Get("Input.Line.SelectEndPoint");
    public static string Input_Line_SelectNextPoint => Get("Input.Line.SelectNextPoint");
    public static string Input_Line_SetDirectionThenTypeLength => Get("Input.Line.SetDirectionThenTypeLength");
    public static string Input_Arc_SelectStart => Get("Input.Arc.SelectStart");
    public static string Input_Arc_SelectEnd => Get("Input.Arc.SelectEnd");
    public static string Input_Arc_SelectRadius => Get("Input.Arc.SelectRadius");
    public static string Input_Rectangle_SelectFirstCorner => Get("Input.Rectangle.SelectFirstCorner");
    public static string Input_Rectangle_SelectOppositeCorner => Get("Input.Rectangle.SelectOppositeCorner");
    public static string Input_Circle_SelectCenter => Get("Input.Circle.SelectCenter");
    public static string Input_Circle_SelectRadius => Get("Input.Circle.SelectRadius");
    public static string Input_Circle_SetDirectionThenTypeRadius => Get("Input.Circle.SetDirectionThenTypeRadius");
    public static string Input_Sector_SelectCenter => Get("Input.Sector.SelectCenter");
    public static string Input_Sector_SelectStart => Get("Input.Sector.SelectStart");
    public static string Input_Sector_SelectAngle => Get("Input.Sector.SelectAngle");
    public static string Input_Sector_SetDirectionThenTypeRadius => Get("Input.Sector.SetDirectionThenTypeRadius");
    public static string Input_Move_Idle => Get("Input.Move.Idle");
    public static string Input_Move_SelectVertices => Get("Input.Move.SelectVertices");
    public static string Input_Move_VertexSelected => Get("Input.Move.VertexSelected");
    public static string Input_Move_SelectDestination => Get("Input.Move.SelectDestination");
    public static string Input_Rotate_Idle => Get("Input.Rotate.Idle");
    public static string Input_Rotate_SelectObjects => Get("Input.Rotate.SelectObjects");
    public static string Input_Rotate_SelectAngle => Get("Input.Rotate.SelectAngle");
    public static string Input_Rotate_SetDirectionThenTypeAngle => Get("Input.Rotate.SetDirectionThenTypeAngle");
    public static string Input_Copy_SelectObjects => Get("Input.Copy.SelectObjects");
    public static string Input_Copy_SelectDestination => Get("Input.Copy.SelectDestination");
    public static string Input_SetDirectionThenTypeDistance => Get("Input.SetDirectionThenTypeDistance");
    public static string Input_Stretch_Idle => Get("Input.Stretch.Idle");
    public static string Input_Stretch_SelectDestination => Get("Input.Stretch.SelectDestination");
    public static string Input_Stretch_SelectBasePoint => Get("Input.Stretch.SelectBasePoint");

    public static string Error_CannotCommand => Get("Error.CannotCommand");
    public static string Error_ArcTooSmall => Get("Error.ArcTooSmall");
    public static string Error_ArcRadiusTooSmall => Get("Error.ArcRadiusTooSmall");
    public static string Error_RectangleTooSmall => Get("Error.RectangleTooSmall");
    public static string Error_CircleTooSmall => Get("Error.CircleTooSmall");
    public static string Error_SectorTooSmall => Get("Error.SectorTooSmall");
    public static string Error_SectorAngleTooSmall => Get("Error.SectorAngleTooSmall");
    public static string Error_StretchUnavailable => Get("Error.StretchUnavailable");

    public static string Selection_NothingSelected => Get("Selection.NothingSelected");
    public static string Selection_Pasted => Get("Selection.Pasted");
    public static string Selection_Edge => Get("Selection.Edge");
    public static string Selection_VertexAt => Get("Selection.VertexAt");
    public static string Selection_PolygonWithArea => Get("Selection.PolygonWithArea");
    public static string Selection_MultipleCount => Get("Selection.MultipleCount");

    public static string Format_Coordinates => Get("Format.Coordinates");
    public static string Format_CoordinatesDefault => Get("Format.CoordinatesDefault");
    public static string Format_Area => Get("Format.Area");
    public static string Format_AreaEmpty => Get("Format.AreaEmpty");

    public static string Action_Gesture_Undo => Get("Action.Gesture.Undo");
    public static string Action_Gesture_Redo => Get("Action.Gesture.Redo");
    public static string Action_Gesture_Cut => Get("Action.Gesture.Cut");
    public static string Action_Gesture_Copy => Get("Action.Gesture.Copy");
    public static string Action_Gesture_Paste => Get("Action.Gesture.Paste");
    public static string Action_Gesture_Delete => Get("Action.Gesture.Delete");
}
