using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P1GravenImage2;

public sealed class UmadP1GravenImage2Ai : IScenarioAi<UmadP1GravenImage2State>
{
    public string Name => "CN PF (1北2南/外内强重力)";

    public void Run(UmadP1GravenImage2State state, SimWorld world)
    {
        var ai = new AiManager(world);
        var cd = state.CountdownSeconds;

        ai.Move(0.1f, OpenerSpots, jitter: 0.2f);
        ai.Move(cd + 5.7f, () => Round1GravitySpots(state), jitter: 0.15f);
        ai.Move(cd + 10.9f, () => Round1RockSpots(state), jitter: 0.2f);
        ai.Move(cd + 15.0f, Tankbuster2Spots, jitter: 0.15f);
        ai.Move(cd + 21.1f, () => Cleave1SafeSpots(state), jitter: 0.2f);
        ai.Move(cd + 24.8f, Round2GravitySpots, jitter: 0.15f);
        ai.Move(cd + 29.4f, () => Round2RockSpots(state), jitter: 0.2f);
        ai.Move(cd + 33.4f, () => Cleave2SafeSpots(state), jitter: 0.2f);
        ai.Move(cd + 37.85f, () => Trap2StackSpots(state), jitter: 0f);
        ai.Move(cd + 41.55f, () => HeavyGravityWave1ApproachSpots(state), jitter: 0f);
        ai.Move(cd + 43.10f, () => HeavyGravityWave1Spots(state), jitter: 0f);
        ai.Move(cd + 45.10f, () => HeavyGravityWave2Spots(state), jitter: 0f);
        ai.Move(cd + 47.60f, RaidwideStackSpots, jitter: 0.2f);
    }

    private static IAiMove OpenerSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -6.0f);
        coords[(int)PartyRole.OffTank] = new Vector2(-2.5f, 4.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.5f, 6.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.5f, 6.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.8f, 4.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.8f, 4.8f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(3.5f, 6.0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-3.5f, 6.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Round1GravitySpots(UmadP1GravenImage2State state)
    {
        var northX = state.IceDiagonalB ? 1.2f : -1.2f;
        var southX = -northX;
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            coords[(int)role] = UmadP1GravenImage2State.IsLightParty1(role)
                ? new Vector2(northX, -18.4f)
                : new Vector2(southX, 18.4f);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Round1RockSpots(UmadP1GravenImage2State state)
        => RockSpreadSpots(dpsGetsGravity: state.R1DpsGetsGravity, stTakingTankbuster: true);

    private static IAiMove Round2RockSpots(UmadP1GravenImage2State state)
        => RockSpreadSpots(dpsGetsGravity: state.R2DpsGetsGravity, stTakingTankbuster: false);

    private static IAiMove RockSpreadSpots(bool dpsGetsGravity, bool stTakingTankbuster)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            var isRock = UmadP1GravenImage2State.IsDps(role) != dpsGetsGravity;
            if (isRock)
            {
                coords[(int)role] = role switch
                {
                    PartyRole.MeleeDpsA or PartyRole.MainTank => new Vector2(9.8f, -0.5f),
                    PartyRole.MeleeDpsB or PartyRole.OffTank => new Vector2(-8.8f, 0.8f),
                    PartyRole.PhysRangedDps or PartyRole.RegenHealer => new Vector2(-11.2f, -6.8f),
                    _ => new Vector2(10.0f, 9.2f),
                };
            }
            else if (stTakingTankbuster && role == PartyRole.OffTank)
            {
                coords[(int)role] = new Vector2(0f, -6.4f);
            }
            else
            {
                coords[(int)role] = role switch
                {
                    PartyRole.MainTank => new Vector2(0.4f, 2.0f),
                    PartyRole.OffTank => new Vector2(-0.4f, 2.0f),
                    PartyRole.RegenHealer => new Vector2(-0.8f, -3.5f),
                    PartyRole.ShieldHealer => new Vector2(0.8f, 7.5f),
                    PartyRole.MeleeDpsA => new Vector2(-0.5f, -3.5f),
                    PartyRole.MeleeDpsB => new Vector2(0.5f, 3.0f),
                    PartyRole.PhysRangedDps => new Vector2(-0.5f, 3.5f),
                    _ => new Vector2(0.5f, 5.5f),
                };
            }
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Tankbuster2Spots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = new Vector2(0f, -6.4f);
        coords[(int)PartyRole.MainTank] = new Vector2(1.2f, 4.8f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.2f, 5.5f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.2f, 7.2f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-0.8f, 4.6f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(0.8f, 5.2f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(-1.6f, 6.2f);
        coords[(int)PartyRole.CasterDps] = new Vector2(1.6f, 7.6f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Cleave1SafeSpots(UmadP1GravenImage2State state)
    {
        var sx = state.Cleave1EastUnsafe ? -2.2f : 2.2f;
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(sx, 5.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(sx * 0.6f, -6.2f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(sx, 5.8f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(sx, 8.2f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(sx, 4.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(sx, 6.8f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(sx, 5.2f);
        coords[(int)PartyRole.CasterDps] = new Vector2(sx, 8.8f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Round2GravitySpots()
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            coords[(int)role] = UmadP1GravenImage2State.IsLightParty1(role)
                ? new Vector2(0f, -12.3f)
                : new Vector2(0f, 12.3f);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Cleave2SafeSpots(UmadP1GravenImage2State state)
    {
        var sx = state.Cleave2EastUnsafe ? -2.4f : 2.4f;
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(sx, -2.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(sx, -0.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(sx, -1.8f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(sx, -3.5f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(sx, 3.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(sx, 4.4f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(sx, 4.0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(sx, 5.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Trap2StackSpots(UmadP1GravenImage2State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            var isDps = UmadP1GravenImage2State.IsDps(role);
            var isHolder = role == (isDps ? state.Trap2Dps : state.Trap2Support);
            coords[(int)role] = (isDps, isHolder) switch
            {
                (false, true) => new Vector2(0f, -6.0f),
                (false, false) => new Vector2(0f, -4.0f),
                (true, true) => new Vector2(0f, 6.0f),
                (true, false) => new Vector2(0f, 4.0f),
            };
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove HeavyGravityWave1ApproachSpots(UmadP1GravenImage2State state)
    {
        var northCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, northHalf: true, new Vector2(0f, -12.3f)), 18.2f);
        var southCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, northHalf: false, new Vector2(0f, 12.3f)), 18.2f);
        var northHold = StageOutsidePuddle(northCenter, 4.5f);
        var southHold = StageOutsidePuddle(southCenter, 4.5f);
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            var isDps = UmadP1GravenImage2State.IsDps(role);
            if (role == state.Trap2Support)
                coords[(int)role] = northHold;
            else if (role == state.Trap2Dps)
                coords[(int)role] = southHold;
            else
                coords[(int)role] = isDps ? northCenter : southCenter;
        }
        return AiMove.Create(coords);
    }

    private static Vector2 StageOutsidePuddle(Vector2 puddleCenter, float clearance)
    {
        var len = puddleCenter.Length();
        if (len <= clearance + 0.5f) return Vector2.Zero;
        return puddleCenter * ((len - clearance) / len);
    }

    private static IAiMove HeavyGravityWave1Spots(UmadP1GravenImage2State state)
    {
        var northCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, northHalf: true, new Vector2(0f, -12.3f)), 18.2f);
        var southCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, northHalf: false, new Vector2(0f, 12.3f)), 18.2f);
        return AssignPostTrapGravitySpots(state, northCenter, southCenter);
    }

    private static IAiMove HeavyGravityWave2Spots(UmadP1GravenImage2State state)
    {
        var northRaw = state.GetHalfAverageDrop(state.R1GravityDrops, northHalf: true, new Vector2(0f, -18.4f));
        var southRaw = state.GetHalfAverageDrop(state.R1GravityDrops, northHalf: false, new Vector2(0f, 18.4f));
        var northCenter = ClampArena(northRaw * 0.90f, 17.2f);
        var southCenter = ClampArena(southRaw * 0.90f, 17.2f);
        return AssignPostTrapGravitySpots(state, northCenter, southCenter);
    }

    private static IAiMove AssignPostTrapGravitySpots(UmadP1GravenImage2State state, Vector2 northCenter, Vector2 southCenter)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            var isDps = UmadP1GravenImage2State.IsDps(role);
            var goesNorth = (isDps && role != state.Trap2Dps) || role == state.Trap2Support;
            coords[(int)role] = goesNorth ? northCenter : southCenter;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove RaidwideStackSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -7.0f);
        coords[(int)PartyRole.OffTank] = new Vector2(0.5f, 6.2f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.2f, 4.5f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.2f, 4.5f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.0f, 5.2f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.0f, 5.2f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(-2.0f, 5.8f);
        coords[(int)PartyRole.CasterDps] = new Vector2(2.0f, 5.8f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 ClampArena(Vector2 p, float maxRadius)
    {
        var len = p.Length();
        return len > maxRadius && len > 0.001f ? p * (maxRadius / len) : p;
    }
}