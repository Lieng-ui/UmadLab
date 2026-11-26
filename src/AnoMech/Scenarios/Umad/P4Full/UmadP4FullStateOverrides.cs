using AnoMech.Scenarios.Umad.P4KefkaSays;

namespace AnoMech.Scenarios.Umad.P4Full;

public sealed class UmadP4FullStateOverrides
{
    public float CountdownSeconds { get; set; } = 5.0f;
    public bool EnableCheatSheet { get; set; } = true;
    public UmadP4KefkaSaysStateOverrides KefkaSays { get; } = new();
}
