namespace AnoMech.Scenarios.Umad.P3Full;

public sealed class UmadP3FullStateOverrides
{
    public float CountdownSeconds { get; set; } = 5.0f;
    public bool? FireFirst { get; set; }
    public bool? LongitudinalFirst { get; set; }
    public int? FireSpot { get; set; }
    public bool? Clockwise { get; set; }
    public int? StartSpot { get; set; }
    public int? PlayerNumber { get; set; }
    public float ChaosKillDelayAfterBigBangSec { get; set; } = 12.5f;
    public float ExdeathKillDelayAfterChaosSec { get; set; } = 6.0f;
    public bool EnableCheatSheet { get; set; } = true;
}
