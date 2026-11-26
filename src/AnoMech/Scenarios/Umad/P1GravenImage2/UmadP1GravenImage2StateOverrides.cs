using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Umad.P1GravenImage2;

public sealed class UmadP1GravenImage2StateOverrides
{
    public float CountdownSeconds { get; set; } = 5f;
    public bool? R1DpsGetsGravity { get; set; }
    public bool? IceDiagonalB { get; set; }
    public bool? IceIsLie { get; set; }
    public bool? R2DpsGetsGravity { get; set; }
    public bool? Cleave1EastUnsafe { get; set; }
    public bool? Cleave2EastUnsafe { get; set; }
    public PartyRole? Trap2Support { get; set; }
    public PartyRole? Trap2Dps { get; set; }
    public bool PropsBindDirector { get; set; } = true;
}