using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Umad.P1GravenImage2;

public sealed class UmadP1GravenImage2State
{
    public static readonly PartyRole[] SupportRoles =
        [PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer];

    public static readonly PartyRole[] DpsRoles =
        [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];

    public static readonly PartyRole[] LightParty1 =
        [PartyRole.MainTank, PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.PhysRangedDps];

    public static readonly PartyRole[] LightParty2 =
        [PartyRole.OffTank, PartyRole.ShieldHealer, PartyRole.MeleeDpsB, PartyRole.CasterDps];

    public static readonly PartyRole[] AllRoles =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    public float CountdownSeconds { get; }

    public bool R1DpsGetsGravity { get; }

    // Ice cones come in two opposite-quadrant pairs:
    // false -> real cones at NE (-pi/4) and SW (+3pi/4), safe quadrants NW and SE.
    // true  -> real cones at NW (-3pi/4) and SE (+pi/4), safe quadrants NE and SW.
    public bool IceDiagonalB { get; }
    public bool IceIsLie { get; }

    public bool R2DpsGetsGravity { get; }

    // Half-room cleaves from the statue:
    // false -> GravityWave (0xBAB1, heading -pi/2, West x < 0 unsafe, East safe).
    // true  -> CrushingAura (0xBAB2, heading +pi/2, East x > 0 unsafe, West safe).
    public bool Cleave1EastUnsafe { get; }
    public bool Cleave2EastUnsafe { get; }

    public PartyRole Trap2Support { get; }
    public PartyRole Trap2Dps { get; }

    private readonly List<Vector2> r1GravityDrops = [];
    private readonly List<Vector2> r2GravityDrops = [];

    public IReadOnlyList<Vector2> R1GravityDrops => r1GravityDrops;
    public IReadOnlyList<Vector2> R2GravityDrops => r2GravityDrops;

    public static bool IsDps(PartyRole role) => role is PartyRole.MeleeDpsA or PartyRole.MeleeDpsB
        or PartyRole.PhysRangedDps or PartyRole.CasterDps;

    public static bool IsLightParty1(PartyRole role) => role is PartyRole.MainTank or PartyRole.RegenHealer
        or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;

    public bool IsR1Gravity(PartyRole role) => IsDps(role) == R1DpsGetsGravity;

    public bool IsR2Gravity(PartyRole role) => IsDps(role) == R2DpsGetsGravity;

    public void ClearGravityDrops()
    {
        r1GravityDrops.Clear();
        r2GravityDrops.Clear();
    }

    public void RecordR1GravityDrop(Vector2 spot) => r1GravityDrops.Add(spot);

    public void RecordR2GravityDrop(Vector2 spot) => r2GravityDrops.Add(spot);

    public Vector2 GetHalfAverageDrop(IReadOnlyList<Vector2> drops, bool northHalf, Vector2 fallback)
    {
        var sum = Vector2.Zero;
        var count = 0;
        for (var i = 0; i < drops.Count; i++)
        {
            var isNorth = drops[i].Y < 0f;
            if (isNorth != northHalf) continue;
            sum += drops[i];
            count++;
        }
        return count > 0 ? sum / count : fallback;
    }

    public UmadP1GravenImage2State(Rng rng, UmadP1GravenImage2StateOverrides overrides)
    {
        CountdownSeconds = overrides.CountdownSeconds < 0f ? 0f : overrides.CountdownSeconds;
        R1DpsGetsGravity = overrides.R1DpsGetsGravity ?? rng.NextBool();
        IceDiagonalB = overrides.IceDiagonalB ?? rng.NextBool();
        IceIsLie = overrides.IceIsLie ?? rng.NextBool();
        R2DpsGetsGravity = overrides.R2DpsGetsGravity ?? rng.NextBool();
        Cleave1EastUnsafe = overrides.Cleave1EastUnsafe ?? rng.NextBool();
        Cleave2EastUnsafe = overrides.Cleave2EastUnsafe ?? rng.NextBool();
        Trap2Support = overrides.Trap2Support ?? rng.NextSupportRole();
        Trap2Dps = overrides.Trap2Dps ?? rng.NextDpsRole();
    }
}