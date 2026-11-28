using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P1GravenImage1;
using AnoMech.Scenarios.Umad.P1GravenImage2;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;

namespace AnoMech.Scenarios.Umad.P1Full;

public sealed class UmadP1FullAi : IScenarioAi<UmadP1FullState>
{
    public string Name => "CN PF Full P1 (盗火/外内泥/回转寿司)";

    private const float Gi2Offset = 78.0f;
    private const float Gi3Offset = 146.0f;
    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private const float IceSafeMarginRad = 0.12f;
    private const float LineClearanceMargin = 1.6f;

    private static readonly Vector2 UpperStatuePos2 = new(0f, -44f);
    private static readonly float LineBaseRotation = UmadP1TeleTrouncingScenario.ThunderAnchors[0].Rotation;
    private static readonly Vector2[] LineAnchors = DeriveLineAnchors();

    private static readonly PartyRole[] WestHalfOrder =
    [
        PartyRole.ShieldHealer,
        PartyRole.RegenHealer,
        PartyRole.OffTank,
        PartyRole.MainTank,
    ];

    private static readonly PartyRole[] EastHalfOrder =
    [
        PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB,
        PartyRole.PhysRangedDps,
        PartyRole.CasterDps,
    ];

    private static readonly Dictionary<PartyRole, float> HorizontalLineX = new()
    {
        [PartyRole.ShieldHealer] = -17.2f,
        [PartyRole.RegenHealer] = -11.6f,
        [PartyRole.OffTank] = -6.8f,
        [PartyRole.MainTank] = -2.2f,
        [PartyRole.MeleeDpsA] = 2.2f,
        [PartyRole.MeleeDpsB] = 6.8f,
        [PartyRole.PhysRangedDps] = 11.6f,
        [PartyRole.CasterDps] = 17.2f,
    };

    public void Run(UmadP1FullState state, SimWorld world)
    {
        var ai = new AiManager(world);
        var party = world.Party;
        var cd = state.CountdownSeconds;
        var g1 = state.Gi1;
        var g2 = state.Gi2;
        var g3 = state.Gi3;
        var gi3AssignedSpots = new Dictionary<PartyRole, Vector2>();

        ai.Move(0.1f, OpenerSpots, jitter: 0.25f);
        ai.Move(cd + 30.5f, () => Gi1PreKnockbackSpots(g1), jitter: 0.1f);
        ai.Move(cd + 36.85f, () => Gi1Magic1ResolveSpots(g1), jitter: 0f);
        ai.Move(cd + 39.45f, Gi1HorizontalLineupSpots, jitter: 0.1f);
        ai.Move(cd + 43.65f, () => Gi1TowerSoakSpots(g1), jitter: 0f);
        ai.Move(cd + 47.25f, () => Gi1Trap1StackSpots(g1), jitter: 0f);
        ai.Move(cd + 51.60f, () => Gi1Magic2SafeSpots(g1), jitter: 0f);
        ai.Move(cd + 52.20f, () => Gi1Magic2SafeSpots(g1), jitter: 0f);
        ai.Move(cd + 55.6f, Gi1RaidwideStackSpots, jitter: 0.2f);
        ai.Move(cd + 64.5f, Gi1OverdriveSpots, jitter: 0.2f);

        var t2 = cd + Gi2Offset;
        ai.Move(t2 + 0.5f, OpenerSpots, jitter: 0.2f);
        ai.Move(t2 + 4.3f, () => Gi2Round1GravitySpots(g2), jitter: 0f);
        ai.Move(t2 + 10.75f, () => Gi2RockSpreadSpots(g2.R1DpsGetsGravity), jitter: 0.1f);
        ai.Move(t2 + 14.80f, Gi2Tankbuster2Spots, jitter: 0.15f);
        ai.Move(t2 + 21.05f, () => Gi2Cleave1SafeSpots(g2), jitter: 0.1f);
        ai.Move(t2 + 24.60f, () => Gi2Round2GravitySpots(g2), jitter: 0f);
        ai.Move(t2 + 29.20f, () => Gi2RockSpreadSpots(g2.R2DpsGetsGravity), jitter: 0.1f);
        ai.Move(t2 + 33.25f, () => Gi2Cleave2SafeSpots(g2), jitter: 0.1f);
        ai.Move(t2 + 37.85f, () => Gi2Trap2StackSpots(g2), jitter: 0f);
        ai.Move(t2 + 41.55f, () => Gi2HeavyGravityWave1ApproachSpots(g2), jitter: 0f);
        ai.Move(t2 + 43.10f, () => Gi2HeavyGravityWave1Spots(g2), jitter: 0f);
        ai.Move(t2 + 45.10f, () => Gi2HeavyGravityWave2Spots(g2), jitter: 0f);
        ai.Move(t2 + 47.60f, Gi2RaidwideStackSpots, jitter: 0.2f);

        var t3 = cd + Gi3Offset;
        ai.Move(t3 + 7.6f, () => Gi3ArrowDropSpots(g3, firstDrop: true), jitter: 0f);
        ai.Move(t3 + 14.50f, () => Gi3ArrowDropSpots(g3, firstDrop: false), jitter: 0f);
        ai.Move(t3 + 17.55f, () => Gi3Confetti3StackSpots(g3), jitter: 0.1f);
        ai.Move(t3 + 23.55f, Gi3CardinalStagingSpots, jitter: 0f);
        ai.Move(t3 + 26.0f, Gi3CardinalSandwichSpots, jitter: 0f);
        ai.Move(t3 + 35.15f, () => Gi3MysteryMagicSpots(g3, party, gi3AssignedSpots, preTurnStep: true), jitter: 0f);
        ai.Move(t3 + 40.30f, () => Gi3MysteryMagicSpots(g3, party, gi3AssignedSpots, preTurnStep: false), jitter: 0f);
    }

    private static Vector2[] DeriveLineAnchors()
    {
        var anchors = UmadP1TeleTrouncingScenario.ThunderAnchors;
        var result = new Vector2[anchors.Length];
        for (var i = 0; i < anchors.Length; i++)
            result[i] = new Vector2(anchors[i].Position.X, anchors[i].Position.Z);
        return result;
    }

    private static IAiMove OpenerSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -6f);
        coords[(int)PartyRole.OffTank] = new Vector2(-2.5f, 4.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.5f, 6f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.5f, 6f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.8f, 4.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.8f, 4.8f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(3.5f, 6f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-3.5f, 6f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Gi1PreKnockbackSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            var target = Gi1Magic1RoleSpot(state, role);
            if (!state.IsTethered(role))
            {
                coords[(int)role] = target;
                continue;
            }
            var dir = target - UpperStatuePos2;
            var dist = dir.Length();
            var pre = dist > 10.5f ? target - dir * (10.0f / dist) : new Vector2(target.X * 0.8f, target.Y - 9.5f);
            coords[(int)role] = ClampArena(pre, 17.5f);
        }
        return AiMove.Create(coords);
    }

    private static IAiMove Gi1Magic1ResolveSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = Gi1Magic1RoleSpot(state, role);
        return AiMove.Create(coords);
    }

    private static Vector2 Gi1Magic1RoleSpot(UmadP1GravenImage1State state, PartyRole role)
    {
        var isDps = UmadP1GravenImage1State.IsDps(role);
        if (state.Fire1IsStack)
            return SafeQuadrantStackSpot(state.Ice1DiagonalB, eastSide: isDps);

        if (state.Ice1DiagonalB)
        {
            return role switch
            {
                PartyRole.MainTank => new Vector2(-6.2f, 1.2f),
                PartyRole.OffTank => new Vector2(-1.2f, 7.0f),
                PartyRole.RegenHealer => new Vector2(-6.5f, 15.0f),
                PartyRole.ShieldHealer => new Vector2(-15.0f, 9.5f),
                PartyRole.MeleeDpsA => new Vector2(6.2f, -1.2f),
                PartyRole.MeleeDpsB => new Vector2(1.2f, -7.0f),
                PartyRole.PhysRangedDps => new Vector2(9.2f, -12.0f),
                _ => new Vector2(16.5f, -2.5f),
            };
        }

        return role switch
        {
            PartyRole.MainTank => new Vector2(-1.2f, -7.0f),
            PartyRole.OffTank => new Vector2(-6.2f, -1.2f),
            PartyRole.RegenHealer => new Vector2(-10.0f, -9.2f),
            PartyRole.ShieldHealer => new Vector2(-17.0f, -1.8f),
            PartyRole.MeleeDpsA => new Vector2(1.2f, 7.0f),
            PartyRole.MeleeDpsB => new Vector2(6.4f, 1.2f),
            PartyRole.PhysRangedDps => new Vector2(10.0f, 9.2f),
            _ => new Vector2(16.5f, 2.0f),
        };
    }

    private static Vector2 SafeQuadrantStackSpot(bool diagonalB, bool eastSide)
        => new(eastSide ? 5.5f : -5.2f, SafeQuadrantSignZ(diagonalB, eastSide) * 2.5f);

    private static float SafeQuadrantSignZ(bool diagonalB, bool eastSide)
        => diagonalB ? (eastSide ? -1f : 1f) : (eastSide ? 1f : -1f);

    private static IAiMove Gi1HorizontalLineupSpots()
    {
        var coords = new Vector2?[8];
        foreach (var (role, x) in HorizontalLineX) coords[(int)role] = new Vector2(x, 0f);
        return AiMove.Create(coords);
    }

    private static IAiMove Gi1TowerSoakSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        AssignHalfTowerSpots(state, WestHalfOrder, coords);
        AssignHalfTowerSpots(state, EastHalfOrder, coords);
        return AiMove.Create(coords);
    }

    private static void AssignHalfTowerSpots(UmadP1GravenImage1State state, PartyRole[] halfOrder, Vector2?[] coords)
    {
        var towerSpots = new List<Vector2>(2);
        var soakers = new List<PartyRole>(2);
        foreach (var role in halfOrder)
        {
            var x = HorizontalLineX[role];
            if (state.IsLaserTarget(role))
            {
                var spot = state.GetTowerSpot(role, new Vector2(x, 0f));
                towerSpots.Add(spot);
                coords[(int)role] = new Vector2(spot.X, Math.Clamp(spot.Y + 5.0f, -15f, 15f));
            }
            else
            {
                soakers.Add(role);
            }
        }
        for (var i = 0; i < soakers.Count && i < towerSpots.Count; i++)
            coords[(int)soakers[i]] = towerSpots[i];
    }

    private static IAiMove Gi1Trap1StackSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            var isDps = UmadP1GravenImage1State.IsDps(role);
            var isHolder = role == (isDps ? state.Trap1Dps : state.Trap1Support);
            coords[(int)role] = (isDps, isHolder) switch
            {
                (false, true) => new Vector2(-7.0f, 0f),
                (false, false) => new Vector2(-5.2f, 0f),
                (true, true) => new Vector2(7.0f, 0f),
                (true, false) => new Vector2(5.2f, 0f),
            };
        }
        return AiMove.Create(coords);
    }

    private static IAiMove Gi1Magic2SafeSpots(UmadP1GravenImage1State state)
    {
        var westSpot = FindNearestIceThunderSafeSpot(state, new Vector2(-9.0f, 0f));
        var eastSpot = FindNearestIceThunderSafeSpot(state, new Vector2(9.0f, 0f));
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            var isDps = UmadP1GravenImage1State.IsDps(role);
            var isHolder = role == (isDps ? state.Trap1Dps : state.Trap1Support);
            var landsEast = (isDps && isHolder) || (!isDps && !isHolder);
            coords[(int)role] = landsEast ? eastSpot : westSpot;
        }
        return AiMove.Create(coords);
    }

    private static Vector2 FindNearestIceThunderSafeSpot(UmadP1GravenImage1State state, Vector2 anchor)
    {
        var best = anchor;
        var bestDist = float.PositiveInfinity;
        for (var r = 2.0f; r <= 18.0f; r += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 5)
            {
                var rad = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(rad) * r, MathF.Cos(rad) * r);
                if (!IsSafeFromIceAndThunder(q, state)) continue;
                var d = Vector2.DistanceSquared(q, anchor);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = q;
                }
            }
        }
        return best;
    }

    private static bool IsSafeFromIceAndThunder(Vector2 q, UmadP1GravenImage1State state)
    {
        if (q.Length() > 18.5f) return false;
        if (!IsSafeFromIce(q, state.Ice2DiagonalB)) return false;
        return LineClearance(q, state.ThunderRealOffset, state.ThunderOrientation) >= LineClearanceMargin;
    }

    private static bool IsSafeFromIce(Vector2 q, bool diagonalB)
    {
        if (q.LengthSquared() < 1.0f) return false;
        var angle = MathF.Atan2(q.X, q.Y);
        var h1 = diagonalB ? -3f * MathF.PI / 4f : -MathF.PI / 4f;
        var h2 = diagonalB ? MathF.PI / 4f : 3f * MathF.PI / 4f;
        var halfCone = MathF.PI / 4f + IceSafeMarginRad;
        return AngleDiff(angle, h1) > halfCone && AngleDiff(angle, h2) > halfCone;
    }

    private static float AngleDiff(float a, float b)
    {
        var d = (a - b) % (2f * MathF.PI);
        if (d > MathF.PI) d -= 2f * MathF.PI;
        if (d < -MathF.PI) d += 2f * MathF.PI;
        return MathF.Abs(d);
    }

    private static float LineClearance(Vector2 q, int realOffset, float orientation)
    {
        var min = float.PositiveInfinity;
        for (var i = 0; i < 4; i++)
        {
            if ((i + realOffset) % 2 != 0) continue;
            var anchor = new Vector2(LineAnchors[i].X * orientation, LineAnchors[i].Y);
            var rot = LineBaseRotation * orientation;
            var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
            var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
            var rel = q - anchor;
            var along = Vector2.Dot(rel, fwd);
            if (along < 0f || along > LineLength) continue;
            min = MathF.Min(min, MathF.Abs(Vector2.Dot(rel, right)) - LineHalfWidth);
        }
        return min;
    }

    private static IAiMove Gi1RaidwideStackSpots()
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = role == PartyRole.MainTank ? new Vector2(0f, -4f) : new Vector2(0f, 5f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Gi1OverdriveSpots()
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = role == PartyRole.OffTank ? new Vector2(-4f, -4f) : new Vector2(0f, 5f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 Gi2RockRoleSpot(PartyRole role) => role switch
    {
        PartyRole.MeleeDpsA or PartyRole.MainTank => new Vector2(10.5f, -5.5f),
        PartyRole.PhysRangedDps or PartyRole.RegenHealer => new Vector2(-10.5f, -5.5f),
        PartyRole.MeleeDpsB or PartyRole.OffTank => new Vector2(-10.5f, 5.5f),
        _ => new Vector2(10.5f, 5.5f),
    };

    private static IAiMove Gi2Round1GravitySpots(UmadP1GravenImage2State state)
    {
        var northX = state.IceDiagonalB ? 2.2f : -2.2f;
        var southX = -northX;
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
            coords[(int)role] = UmadP1GravenImage2State.IsLightParty1(role)
                ? new Vector2(northX, -18.4f)
                : new Vector2(southX, 18.4f);
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2RockSpreadSpots(bool dpsGetsGravity)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            var isRock = UmadP1GravenImage2State.IsDps(role) != dpsGetsGravity;
            if (isRock)
            {
                coords[(int)role] = Gi2RockRoleSpot(role);
            }
            else
            {
                coords[(int)role] = role switch
                {
                    PartyRole.MainTank => new Vector2(0.5f, -4.5f),
                    PartyRole.RegenHealer => new Vector2(-0.5f, -4.5f),
                    PartyRole.MeleeDpsA => new Vector2(0.5f, -4.5f),
                    PartyRole.PhysRangedDps => new Vector2(-0.5f, -4.5f),
                    PartyRole.OffTank => new Vector2(-0.5f, 4.5f),
                    PartyRole.ShieldHealer => new Vector2(0.5f, 4.5f),
                    PartyRole.MeleeDpsB => new Vector2(-0.5f, 4.5f),
                    _ => new Vector2(0.5f, 4.5f),
                };
            }
        }
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2Tankbuster2Spots()
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

    private static IAiMove Gi2Cleave1SafeSpots(UmadP1GravenImage2State state)
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
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2Round2GravitySpots(UmadP1GravenImage2State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
            coords[(int)role] = UmadP1GravenImage2State.IsLightParty1(role)
                ? new Vector2(0f, -12.3f)
                : new Vector2(0f, 12.3f);
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2Cleave2SafeSpots(UmadP1GravenImage2State state)
    {
        var sx = state.Cleave2EastUnsafe ? -2.4f : 2.4f;
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(sx, -2.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(sx, -0.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(sx, -2.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(sx, 2.5f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(sx, 2.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(sx, 3.6f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(sx, -2.2f);
        coords[(int)PartyRole.CasterDps] = new Vector2(sx, 4.5f);
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2Trap2StackSpots(UmadP1GravenImage2State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage2State.AllRoles)
        {
            var isDps = UmadP1GravenImage2State.IsDps(role);
            var isHolder = role == (isDps ? state.Trap2Dps : state.Trap2Support);
            coords[(int)role] = (isDps, isHolder) switch
            {
                (false, true) => new Vector2(0f, -6.5f),
                (false, false) => new Vector2(0f, -4.5f),
                (true, true) => new Vector2(0f, 6.5f),
                (true, false) => new Vector2(0f, 4.5f),
            };
        }
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2HeavyGravityWave1ApproachSpots(UmadP1GravenImage2State state)
    {
        var northCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, true, new Vector2(0f, -12.3f)), 18.2f);
        var southCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, false, new Vector2(0f, 12.3f)), 18.2f);
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

    private static IAiMove Gi2HeavyGravityWave1Spots(UmadP1GravenImage2State state)
    {
        var northCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, true, new Vector2(0f, -12.3f)), 18.2f);
        var southCenter = ClampArena(state.GetHalfAverageDrop(state.R2GravityDrops, false, new Vector2(0f, 12.3f)), 18.2f);
        return AssignPostTrapGravitySpots(state, northCenter, southCenter);
    }

    private static IAiMove Gi2HeavyGravityWave2Spots(UmadP1GravenImage2State state)
    {
        var northCenter = ClampArena(state.GetHalfAverageDrop(state.R1GravityDrops, true, new Vector2(0f, -18.4f)), 18.4f);
        var southCenter = ClampArena(state.GetHalfAverageDrop(state.R1GravityDrops, false, new Vector2(0f, 18.4f)), 18.4f);
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
        return AiMove.Create(coords);
    }

    private static IAiMove Gi2RaidwideStackSpots()
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

    private static IAiMove Gi3ArrowDropSpots(UmadP1TeleTrouncingState state, bool firstDrop)
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
        return AiMove.Create(coords);
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

    private static IAiMove Gi3Confetti3StackSpots(UmadP1TeleTrouncingState state)
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
        return AiMove.Create(coords);
    }

    private static IAiMove Gi3CardinalStagingSpots()
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
        return AiMove.Create(coords);
    }

    private static IAiMove Gi3CardinalSandwichSpots()
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
        return AiMove.Create(coords);
    }

    private static IAiMove Gi3MysteryMagicSpots(
        UmadP1TeleTrouncingState state,
        SimParty party,
        Dictionary<PartyRole, Vector2> assignedSpots,
        bool preTurnStep)
    {
        var dirN4 = (state.ThunderOrientation > 0f, state.ThunderRealOffset == 0) switch
        {
            (true, false) => 0,
            (false, true) => 1,
            (true, true) => 2,
            _ => 3,
        };
        var turnOffset = preTurnStep
            ? new Vector2(0f, state.GazeInverted ? 0.35f : -0.35f)
            : Vector2.Zero;

        var coords = new Vector2?[8];
        if (state.FireIsStack)
        {
            var supportSpot = dirN4 switch
            {
                0 => new Vector2(-5.0f, -5.0f),
                1 => new Vector2(-7.3f, -3.0f),
                2 => new Vector2(-9.2f, -9.2f),
                _ => new Vector2(-3.0f, -7.3f),
            } + turnOffset;
            var dpsSpot = dirN4 switch
            {
                0 => new Vector2(9.2f, 9.2f),
                1 => new Vector2(3.0f, 7.3f),
                2 => new Vector2(5.0f, 5.0f),
                _ => new Vector2(7.3f, 3.0f),
            } + turnOffset;
            foreach (var role in state.Debuffs.Keys)
                coords[(int)role] = UmadP1TeleTrouncingState.IsDps(role) ? dpsSpot : supportSpot;
            return AiMove.Create(coords);
        }

        var rotAngle = -MathF.PI / 4f - dirN4 * (MathF.PI / 2f);
        var cos = MathF.Cos(rotAngle);
        var sin = MathF.Sin(rotAngle);
        foreach (var role in state.Debuffs.Keys)
        {
            var baseDir = role switch
            {
                PartyRole.MainTank => 1,
                PartyRole.OffTank => 3,
                PartyRole.RegenHealer => 0,
                PartyRole.ShieldHealer => 2,
                PartyRole.MeleeDpsA => 7,
                PartyRole.MeleeDpsB => 5,
                PartyRole.PhysRangedDps => 4,
                _ => 6,
            };
            var relDir8 = ((baseDir - 2 * dirN4 - 1) % 8 + 8) % 8;
            var local = relDir8 switch
            {
                0 => new Vector2(0.0f, -8.0f),
                1 => new Vector2(-9.0f, -8.5f),
                2 => new Vector2(-8.0f, -1.5f),
                3 => new Vector2(-11.5f, 11.5f),
                4 => new Vector2(0.0f, 11.0f),
                5 => new Vector2(11.5f, 11.5f),
                6 => new Vector2(8.0f, -1.5f),
                _ => new Vector2(9.0f, -8.5f),
            };
            var worldSpot = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
            coords[(int)role] = worldSpot + turnOffset;
        }

        return AiMove.Create(coords);
    }
}
