using LiteCad.Tools;

namespace LiteCad.Services;

public static class LinearInputCommit
{
    public static bool TryCommitLength(ITool tool, string input, LinearDisplayUnit unit, LineInputLabelMode mode)
    {
        if (mode == LineInputLabelMode.Angle)
        {
            return tool.TryApplyLengthInput(input);
        }

        if (mode == LineInputLabelMode.Offset)
        {
            if (!LinearInputParser.TryParse(input, unit, allowNegative: true, allowEmpty: false, out var offset))
            {
                return false;
            }

            return tool.TryApplyLength(offset);
        }

        if (mode is LineInputLabelMode.ArcHeight)
        {
            if (!LinearInputParser.TryParsePositiveDistance(input, unit, out var arcHeight))
            {
                return false;
            }

            return tool.TryApplyLength(arcHeight);
        }

        if (!LinearInputParser.TryParsePositiveDistance(input, unit, out var length))
        {
            return false;
        }

        return tool.TryApplyLength(length);
    }

    public static bool TryCommitRectangleSize(
        ITool tool,
        string width,
        string height,
        LinearDisplayUnit unit,
        DualFieldLabelMode dualFieldLabelMode)
    {
        if (dualFieldLabelMode == DualFieldLabelMode.MoveOffset)
        {
            if (!LinearInputParser.TryParseSigned(width, unit, out var deltaX)
                || !LinearInputParser.TryParseSigned(height, unit, out var deltaY))
            {
                return false;
            }

            return tool.TryApplyRectangleSize(
                deltaX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                deltaY.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!LinearInputParser.TryParsePositiveDistance(width, unit, out var parsedWidth)
            || !LinearInputParser.TryParsePositiveDistance(height, unit, out var parsedHeight))
        {
            return false;
        }

        return tool.TryApplyRectangleSize(
            parsedWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            parsedHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
