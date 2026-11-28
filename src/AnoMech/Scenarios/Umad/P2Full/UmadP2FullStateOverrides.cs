using AnoMech.Scenarios.Umad.P2Forsaken;

namespace AnoMech.Scenarios.Umad.P2Full;

public sealed class UmadP2FullStateOverrides
{
    public float CountdownSeconds { get; set; } = 5.0f;
    public UmadP2ForsakenStateOverrides Forsaken { get; } = new();
    public bool? FirstWingsEastUnsafe { get; set; }
    public bool? TrineCenterIsRight { get; set; }
    public bool PropsBindDirector { get; set; } = true;
}
