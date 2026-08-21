namespace LiteCad.Services;

public enum LinearDisplayUnit
{
    Millimeters,
    Meters
}

public sealed class DisplayUnitSettings
{
    private LinearDisplayUnit _linearUnit = LinearDisplayUnit.Millimeters;

    public LinearDisplayUnit LinearUnit
    {
        get => _linearUnit;
        set
        {
            if (_linearUnit == value)
            {
                return;
            }

            _linearUnit = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
