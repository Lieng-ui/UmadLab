using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullState
{
    private static readonly PartyRole[] NonTankRoles =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    public float CountdownSeconds { get; }
    public float PreTargetableSec { get; }
    public float ClearTimeSec { get; }
    public UmadP5FloodState Flood { get; }
    public UmadP5CelestriadState Celestriad { get; }
    public UmadP5ExaflaresState Exaflares { get; }

    public IReadOnlyList<PartyRole> Orchestra1Wave1Holy { get; }
    public PartyRole Orchestra1FlareTank { get; }
    public PartyRole Orchestra1HolyTank { get; }

    public IReadOnlyList<PartyRole> Orchestra2Wave1Holy { get; }
    public PartyRole Orchestra2FlareTank { get; }
    public PartyRole Orchestra2HolyTank { get; }

    public IReadOnlyList<PartyRole> ForsakenStackTargets { get; }

    public UmadP5FullState(SimWorld world, UmadP5FullStateOverrides overrides, float countdownSeconds, float preTargetableSec)
    {
        CountdownSeconds = countdownSeconds;
        PreTargetableSec = preTargetableSec;
        ClearTimeSec = Math.Clamp(overrides.ClearTimeSec, 160.0f, 220.0f);
        Flood = new UmadP5FloodState(world.Rng, overrides.Flood);
        Celestriad = new UmadP5CelestriadState(world.Rng, world.Party, overrides.Celestriad);
        Exaflares = new UmadP5ExaflaresState(world.Rng, overrides.Exaflares);

        Orchestra1Wave1Holy = world.Rng.Shuffle(NonTankRoles).Take(3).ToArray();
        var mtFlareFirst = world.Rng.NextBool();
        Orchestra1FlareTank = mtFlareFirst ? PartyRole.MainTank : PartyRole.OffTank;
        Orchestra1HolyTank = mtFlareFirst ? PartyRole.OffTank : PartyRole.MainTank;

        Orchestra2Wave1Holy = world.Rng.Shuffle(NonTankRoles).Take(3).ToArray();
        Orchestra2FlareTank = Orchestra1HolyTank;
        Orchestra2HolyTank = Orchestra1FlareTank;

        ForsakenStackTargets =
        [
            world.Rng.NextRole(),
            world.Rng.NextRole(),
            world.Rng.NextRole(),
            world.Rng.NextRole(),
        ];
    }
}
