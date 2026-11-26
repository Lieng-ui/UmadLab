using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

public sealed class UmadP1TeleTrouncingCnPfAi : IScenarioAi<UmadP1TeleTrouncingState>
{
    public string Name => "CN PF (回转寿司/盗火大圈)";

    public void Run(UmadP1TeleTrouncingState state, SimWorld world)
    {
        var ai = new AiManager(world);

        ai.Move(0.2f, OpenerSpots, jitter: 0.2f);
        ai.Move(7.8f, () => ArrowDropSpots(state, firstDrop: true), jitter: 0f);
        ai.Move(14.6f, () => ArrowDropSpots(state, firstDrop: false), jitter: 0f);
        ai.Move(17.7f, () => Confetti3StackSpots(state), jitter: 0.1f);
        ai.Move(23.9f, CardinalStagingSpots, jitter: 0f);
        ai.Move(26.2f, CardinalSandwichSpots, jitter: 0f);
        ai.Move(35.3f, () => MysteryMagicSpots(state, preTurnStep: true), jitter: 0f);
        ai.Move(40.2f, () => MysteryMagicSpots(state, preTurnStep: false), jitter: 0f);
    }

    private static IAiMove OpenerSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -6.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(-2.5f, 4.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.5f, 6.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.5f, 6.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.8f, 4.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.8f, 4.8f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(3.5f, 6.0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-3.5f, 6.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove ArrowDropSpots(UmadP1TeleTrouncingState state, bool firstDrop)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            var (at7, at10) = state.ExpiryDirections(role);
            if (at7 == at10)
            {
                coords[(int)role] = GetSameArrowSpot(at7, firstDrop);
            }
            else
            {
                var activeDir = firstDrop ? at7 : at10;
                var partnerDir = firstDrop ? at10 : at7;
                coords[(int)role] = GetDifferentArrowSpot(activeDir, partnerDir);
            }
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 GetSameArrowSpot(TelePortentDirection dir, bool firstDrop) => dir switch
    {
        TelePortentDirection.Left => firstDrop ? new Vector2(0f, -12f) : new Vector2(6f, -12f),
        TelePortentDirection.Down => firstDrop ? new Vector2(-12f, 0f) : new Vector2(-12f, -6f),
        TelePortentDirection.Right => firstDrop ? new Vector2(0f, 12f) : new Vector2(-6f, 12f),
        TelePortentDirection.Up => firstDrop ? new Vector2(12f, 0f) : new Vector2(12f, 6f),
        _ => Vector2.Zero,
    };

    private static Vector2 GetDifferentArrowSpot(TelePortentDirection activeDir, TelePortentDirection partnerDir)
    {
        return (activeDir, partnerDir) switch
        {
            (TelePortentDirection.Left, TelePortentDirection.Up) => new Vector2(12f, -12f),
            (TelePortentDirection.Up, TelePortentDirection.Left) => new Vector2(12f, -6f),

            (TelePortentDirection.Down, TelePortentDirection.Left) => new Vector2(-12f, -12f),
            (TelePortentDirection.Left, TelePortentDirection.Down) => new Vector2(-6f, -12f),

            (TelePortentDirection.Right, TelePortentDirection.Down) => new Vector2(-12f, 12f),
            (TelePortentDirection.Down, TelePortentDirection.Right) => new Vector2(-12f, 6f),

            (TelePortentDirection.Up, TelePortentDirection.Right) => new Vector2(12f, 12f),
            (TelePortentDirection.Right, TelePortentDirection.Up) => new Vector2(6f, 12f),

            _ => GetSameArrowSpot(activeDir, firstDrop: true),
        };
    }

    private static IAiMove Confetti3StackSpots(UmadP1TeleTrouncingState state)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            var isDps = UmadP1TeleTrouncingState.IsDps(role);
            var isHolder = role == (isDps ? state.ConfettiStackDps : state.ConfettiStackSupport);
            coords[(int)role] = (isDps, isHolder) switch
            {
                (false, true) => new Vector2(-6.0f, -6.0f),
                (false, false) => new Vector2(-4.2f, -4.2f),
                (true, true) => new Vector2(6.0f, 6.0f),
                (true, false) => new Vector2(4.2f, 4.2f),
            };
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove CardinalStagingSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -8.0f);
        coords[(int)PartyRole.OffTank] = new Vector2(8.0f, 0f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(0f, 8.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-8.0f, 0f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(-3.0f, -15.5f);
        coords[(int)PartyRole.CasterDps] = new Vector2(15.5f, 3.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(3.0f, 15.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-15.5f, -3.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove CardinalSandwichSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -8.0f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(0f, -15.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(8.0f, 0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(15.5f, 0f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(0f, 8.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(0f, 15.5f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-8.0f, 0f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-15.5f, 0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove MysteryMagicSpots(UmadP1TeleTrouncingState state, bool preTurnStep)
    {
        var innerLane = state.ThunderRealOffset == 0 ? 1 : 2;
        var outerLane = state.ThunderRealOffset == 0 ? 3 : 0;
        var turnOffset = preTurnStep
            ? new Vector2(0f, state.GazeInverted ? 0.35f : -0.35f)
            : Vector2.Zero;

        var coords = new Vector2?[8];
        if (state.FireIsStack)
        {
            var supportSpot = LanePoint(innerLane, -4.5f, state.ThunderOrientation) + turnOffset;
            var dpsSpot = LanePoint(innerLane, 4.5f, state.ThunderOrientation) + turnOffset;
            foreach (var role in state.Debuffs.Keys)
                coords[(int)role] = UmadP1TeleTrouncingState.IsDps(role) ? dpsSpot : supportSpot;
        }
        else
        {
            coords[(int)PartyRole.MainTank] = LanePoint(innerLane, -14.5f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.MeleeDpsA] = LanePoint(innerLane, -7.2f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.MeleeDpsB] = LanePoint(innerLane, 0.0f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.OffTank] = LanePoint(innerLane, 7.2f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.RegenHealer] = LanePoint(innerLane, 14.5f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.PhysRangedDps] = LanePoint(outerLane, -7.8f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.ShieldHealer] = LanePoint(outerLane, 0.0f, state.ThunderOrientation) + turnOffset;
            coords[(int)PartyRole.CasterDps] = LanePoint(outerLane, 7.8f, state.ThunderOrientation) + turnOffset;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 LanePoint(int laneIndex, float along, float orientation)
    {
        const float invSqrt2 = 0.70710678f;
        var perp = 15f - 10f * laneIndex;
        var x = (perp - along) * invSqrt2 * orientation;
        var z = (perp + along) * invSqrt2;
        return new Vector2(x, z);
    }
}