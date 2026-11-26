using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Umad.P1GravenImage1;

// null leaves the field randomized at scenario start.
public sealed class UmadP1GravenImage1StateOverrides
{
    public float CountdownSeconds { get; set; } = 5f;

    public bool? DpsGetsTether { get; set; }

    public bool? Ice1DiagonalB { get; set; }
    public bool? Ice1IsLie { get; set; }

    public bool? Fire1IsStack { get; set; }
    public bool? Fire1IsLie { get; set; }
    public PartyRole? Fire1StackSupport { get; set; }
    public PartyRole? Fire1StackDps { get; set; }

    public PartyRole[]? LaserTargets { get; set; }

    public PartyRole? Trap1Support { get; set; }
    public PartyRole? Trap1Dps { get; set; }

    public bool? Ice2DiagonalB { get; set; }
    public bool? Ice2IsLie { get; set; }

    public int? ThunderRealOffset { get; set; }
    public bool? ThunderOrientationFlipped { get; set; }
    public bool? ThunderIsLie { get; set; }

    public bool PropsBindDirector { get; set; } = true;
}