using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Umad.P1GravenImage1;

public sealed class UmadP1GravenImage1State
{
    public static readonly PartyRole[] SupportRoles =
        [PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer];

    public static readonly PartyRole[] DpsRoles =
        [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];

    public static readonly PartyRole[] AllRoles =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    public float CountdownSeconds { get; }

    public bool DpsGetsTether { get; }

    // Ice cones come in two opposite-quadrant pairs:
    // false -> real cones at NE (-pi/4) and SW (+3pi/4), safe quadrants NW and SE.
    // true  -> real cones at NW (-3pi/4) and SE (+pi/4), safe quadrants NE and SW.
    public bool Ice1DiagonalB { get; }
    public bool Ice1IsLie { get; }

    public bool Fire1IsStack { get; }
    public bool Fire1IsLie { get; }
    public bool Fire1ShowsStackIcon => Fire1IsStack ^ Fire1IsLie;
    public PartyRole Fire1StackSupport { get; }
    public PartyRole Fire1StackDps { get; }

    public IReadOnlyList<PartyRole> LaserTargets { get; }
    public IReadOnlyList<PartyRole> TowerSoakers { get; }

    public PartyRole Trap1Support { get; }
    public PartyRole Trap1Dps { get; }

    public bool Ice2DiagonalB { get; }
    public bool Ice2IsLie { get; }

    public int ThunderRealOffset { get; }
    public bool ThunderOrientationFlipped { get; }
    public bool ThunderIsLie { get; }
    public float ThunderOrientation => ThunderOrientationFlipped ? -1f : 1f;

    public static bool IsDps(PartyRole role) => role is PartyRole.MeleeDpsA or PartyRole.MeleeDpsB
        or PartyRole.PhysRangedDps or PartyRole.CasterDps;

    public bool IsTethered(PartyRole role) => IsDps(role) == DpsGetsTether;

    public bool IsLaserTarget(PartyRole role) => LaserTargets.Contains(role);

    private readonly Dictionary<PartyRole, Vector2> towerSpots = [];

    public void RecordTowerSpot(PartyRole role, Vector2 spot) => towerSpots[role] = spot;

    public Vector2 GetTowerSpot(PartyRole role, Vector2 fallback)
        => towerSpots.TryGetValue(role, out var spot) ? spot : fallback;

    public UmadP1GravenImage1State(Rng rng, UmadP1GravenImage1StateOverrides overrides)
    {
        CountdownSeconds = overrides.CountdownSeconds < 0f ? 0f : overrides.CountdownSeconds;

        DpsGetsTether = overrides.DpsGetsTether ?? rng.NextBool();

        Ice1DiagonalB = overrides.Ice1DiagonalB ?? rng.NextBool();
        Ice1IsLie = overrides.Ice1IsLie ?? rng.NextBool();

        Fire1IsStack = overrides.Fire1IsStack ?? rng.NextBool();
        Fire1IsLie = overrides.Fire1IsLie ?? rng.NextBool();
        Fire1StackSupport = overrides.Fire1StackSupport ?? rng.NextSupportRole();
        Fire1StackDps = overrides.Fire1StackDps ?? rng.NextDpsRole();

        var supports = rng.Shuffle(SupportRoles);
        var dps = rng.Shuffle(DpsRoles);
        var laserList = overrides.LaserTargets is { Length: 4 } customLasers
            ? customLasers.ToList()
            : [supports[0], supports[1], dps[0], dps[1]];
        LaserTargets = laserList;
        TowerSoakers = AllRoles.Where(r => !laserList.Contains(r)).ToList();

        Trap1Support = overrides.Trap1Support ?? rng.NextSupportRole();
        Trap1Dps = overrides.Trap1Dps ?? rng.NextDpsRole();

        Ice2DiagonalB = overrides.Ice2DiagonalB ?? rng.NextBool();
        Ice2IsLie = overrides.Ice2IsLie ?? rng.NextBool();

        ThunderRealOffset = overrides.ThunderRealOffset ?? rng.NextInt(2);
        ThunderOrientationFlipped = overrides.ThunderOrientationFlipped ?? rng.NextBool();
        ThunderIsLie = overrides.ThunderIsLie ?? rng.NextBool();
    }
}