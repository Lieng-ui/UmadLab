using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Scenarios.Umad.P3LimitCut;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3Full;

public sealed class UmadP3FullObjects
{
    public SimEnemy? Kefka { get; set; }
    public SimEnemy? Chaos { get; set; }
    public SimEnemy? Exdeath { get; set; }
    public SimEnemy?[] Clones { get; } = new SimEnemy?[8];
}

public sealed class UmadP3FullState
{
    private readonly Rng rng;

    public UmadP3FullObjects Objects { get; } = new();
    public UmadP3BlackHoleState BlackHole { get; }
    public float CountdownSeconds { get; }
    public float ChaosKillDelayAfterBigBangSec { get; }
    public float ExdeathKillDelayAfterChaosSec { get; }
    public bool EnableCheatSheet { get; }

    // Spot indices match UmadP3LimitCutState: 0=S, 1=SE, 2=E, 3=NE, 4=N, 5=NW, 6=W, 7=SW
    public int FireSpot { get; }
    public int WaterSpot { get; }
    public int WindSpot { get; }
    public bool FireFirst { get; }
    public uint ImplosionAttack { get; }

    public int ShortSpot => FireFirst ? FireSpot : WaterSpot;
    public int LongSpot => FireFirst ? WaterSpot : FireSpot;

    public PartyRole FireSupport { get; }
    public PartyRole FireDps { get; }
    public PartyRole WaterSupport { get; }
    public PartyRole WaterDps { get; }
    public IReadOnlyDictionary<PartyRole, Wind> Winds { get; }

    public int StartSpot { get; }
    public bool Clockwise { get; }
    public IReadOnlyList<PartyRole> Numbers { get; }
    public PartyRole BaitRole { get; } = PartyRole.PhysRangedDps;

    public int PlacementStep => Clockwise ? -1 : 1;
    public int PlacementSpot(int k) => Mod8(StartSpot + k * PlacementStep);
    public int ChargeSpot(int k) => Mod8(StartSpot - k * PlacementStep);
    public float SafeHeading(int k) => SpotHeading(ChargeSpot(k)) + MathF.PI - PlacementStep * (MathF.PI / 8f);

    public PartyRole ShortSupport => FireFirst ? FireSupport : WaterSupport;
    public PartyRole ShortDps => FireFirst ? FireDps : WaterDps;
    public PartyRole LongSupport => FireFirst ? WaterSupport : FireSupport;
    public PartyRole LongDps => FireFirst ? WaterDps : FireDps;

    public static float SpotHeading(int spot) => Mod8(spot) * (MathF.PI / 4f);
    public static Vector3 SpotPosition(int spot) => OnCircle3(SpotHeading(spot), 20f);
    public static Vector3 OnCircle3(float heading, float radius) => new(MathF.Sin(heading) * radius, 0f, MathF.Cos(heading) * radius);
    public static Vector2 OnCircle2(float heading, float radius) => new(MathF.Sin(heading) * radius, MathF.Cos(heading) * radius);
    public static int Mod8(int v) => ((v % 8) + 8) % 8;

    public T PickRandom<T>(T[] values) => rng.NextObj(values);

    private static readonly PartyRole[] SupportRoles =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
    ];

    private static readonly PartyRole[] DpsRoles =
    [
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    private static readonly PartyRole[] AllRoles =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    public UmadP3FullState(SimWorld world, UmadP3FullStateOverrides overrides, float countdownSeconds = 5.0f)
    {
        rng = world.Rng;
        var party = world.Party;
        CountdownSeconds = countdownSeconds;
        ChaosKillDelayAfterBigBangSec = Math.Clamp(overrides.ChaosKillDelayAfterBigBangSec, 5.0f, 25.0f);
        ExdeathKillDelayAfterChaosSec = Math.Clamp(overrides.ExdeathKillDelayAfterChaosSec, 2.0f, 9.5f);
        EnableCheatSheet = overrides.EnableCheatSheet;
        FireSpot = overrides.FireSpot is 1 or 3 or 5 or 7 ? overrides.FireSpot.Value : rng.NextObj(1, 3, 5, 7);
        WaterSpot = Mod8(FireSpot + 4);
        WindSpot = Mod8(FireSpot + (rng.NextBool() ? 2 : 6));
        FireFirst = overrides.FireFirst ?? rng.NextBool();
        ImplosionAttack = (overrides.LongitudinalFirst ?? rng.NextBool())
            ? ActionId.LongitudinalImplosion
            : ActionId.LatitudinalImplosion;

        var supports = rng.Shuffle(SupportRoles).ToArray();
        var dps = rng.Shuffle(DpsRoles).ToArray();
        FireSupport = supports[0];
        WaterSupport = supports[1];
        FireDps = dps[0];
        WaterDps = dps[1];

        var headwindTarget = rng.NextInt(100) switch
        {
            < 39 => 4,
            < 71 => 5,
            < 96 => 3,
            _ => 2,
        };
        var shuffled = rng.Shuffle(AllRoles).ToArray();
        var winds = new Dictionary<PartyRole, Wind>(8);
        for (var i = 0; i < shuffled.Length; i++)
            winds[shuffled[i]] = i < headwindTarget ? Wind.Headwind : Wind.Tailwind;
        Winds = winds;

        StartSpot = overrides.StartSpot is >= 0 and <= 7 ? overrides.StartSpot.Value : rng.NextInt(8);
        Clockwise = overrides.Clockwise ?? rng.NextBool();
        var numbers = rng.Shuffle(AllRoles).ToList();
        if (overrides.PlayerNumber is >= 1 and <= 8)
        {
            var wantedIdx = overrides.PlayerNumber.Value - 1;
            var curIdx = numbers.IndexOf(party.PlayerRole);
            if (curIdx >= 0 && curIdx != wantedIdx)
                (numbers[curIdx], numbers[wantedIdx]) = (numbers[wantedIdx], numbers[curIdx]);
        }
        Numbers = numbers;

        var bhOverrides = new UmadP3BlackHoleStateOverrides
        {
            ThunderSet1 = ThunderIIIAssignment.OtInvulnsBoth,
            ThunderSet2 = ThunderIIIAssignment.ShareOtFirst,
        };
        BlackHole = new UmadP3BlackHoleState(world, bhOverrides);
    }

    public bool HasFire(PartyRole role) => role == FireSupport || role == FireDps;
    public bool HasWater(PartyRole role) => role == WaterSupport || role == WaterDps;
}
