using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullStateOverrides
{
    public float CountdownSeconds { get; set; } = 5.0f;
    public float ClearTimeSec { get; set; } = 205.0f;
    public bool PlayOpeningTransitionWhenSolo { get; set; } = false;
    public UmadP5FloodStateOverrides Flood { get; } = new();
    public UmadP5CelestriadStateOverrides Celestriad { get; } = new();
    public UmadP5ExaflaresStateOverrides Exaflares { get; } = new();
}
