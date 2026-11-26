using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;

namespace AnoMech.Scenarios.Umad.P1GravenImage1;

public sealed class UmadP1GravenImage1Ai : IScenarioAi<UmadP1GravenImage1State>
{
    public string Name => "CN PF (盗火烬/一字排开)";

    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private const float IceSafeMarginRad = 0.12f;
    private const float LineClearanceMargin = 1.6f;

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
        [PartyRole.ShieldHealer]  = -17.5f,
        [PartyRole.RegenHealer]   = -11.8f,
        [PartyRole.OffTank]       = -7.0f,
        [PartyRole.MainTank]      = -2.4f,
        [PartyRole.MeleeDpsA]     = 2.4f,
        [PartyRole.MeleeDpsB]     = 7.0f,
        [PartyRole.PhysRangedDps] = 11.8f,
        [PartyRole.CasterDps]     = 17.5f,
    };

    public void Run(UmadP1GravenImage1State state, SimWorld world)
    {
        var ai = new AiManager(world);
        var cd = state.CountdownSeconds;

        ai.Move(0.1f, OpenerSpots, jitter: 0.25f);
        ai.Move(cd + 30.5f, () => PreKnockbackSpots(state), jitter: 0.2f);
        ai.Move(cd + 36.9f, () => Magic1ResolveSpots(state), jitter: 0.15f);
        ai.Move(cd + 39.5f, HorizontalLineupSpots, jitter: 0.15f);
        ai.Move(cd + 43.8f, () => TowerSoakSpots(state), jitter: 0.15f);
        ai.Move(cd + 47.3f, () => Trap1StackSpots(state), jitter: 0.2f);
        ai.Move(cd + 51.6f, () => Magic2SafeSpots(state), jitter: 0.2f);
        ai.Move(cd + 55.6f, RaidwideStackSpots, jitter: 0.3f);
        ai.Move(cd + 64.5f, OverdriveSpots, jitter: 0.25f);
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
        coords[(int)PartyRole.OffTank] = new Vector2(-3f, 5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.5f, 6.5f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.5f, 6.5f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-2f, 5f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(2f, 5f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(4f, 6.5f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-4f, 6.5f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove PreKnockbackSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            var isDps = UmadP1GravenImage1State.IsDps(role);
            var target = Magic1RoleSpot(state, role);
            if (state.IsTethered(role))
                coords[(int)role] = new Vector2(Math.Clamp(target.X * 0.82f, -12f, 12f), Math.Clamp(target.Y - 9.0f, -15f, 4f));
            else
                coords[(int)role] = SafeQuadrantStackSpot(state.Ice1DiagonalB, eastSide: isDps);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Magic1ResolveSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = Magic1RoleSpot(state, role);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 Magic1RoleSpot(UmadP1GravenImage1State state, PartyRole role)
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
    {
        var sx = eastSide ? 5.5f : -5.2f;
        var sz = SafeQuadrantSignZ(diagonalB, eastSide) * 2.5f;
        return new Vector2(sx, sz);
    }

    private static Vector2 SafeQuadrantCenter(bool diagonalB, bool eastSide)
    {
        var sx = eastSide ? 1f : -1f;
        var sz = SafeQuadrantSignZ(diagonalB, eastSide);
        return new Vector2(sx * 6.5f, sz * 6.5f);
    }

    private static float SafeQuadrantSignZ(bool diagonalB, bool eastSide)
        => diagonalB ? (eastSide ? -1f : 1f) : (eastSide ? 1f : -1f);

    private static IAiMove HorizontalLineupSpots()
    {
        var coords = new Vector2?[8];
        foreach (var (role, x) in HorizontalLineX)
            coords[(int)role] = new Vector2(x, 0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove TowerSoakSpots(UmadP1GravenImage1State state)
    {
        var coords = new Vector2?[8];
        AssignHalfTowerSpots(state, WestHalfOrder, coords);
        AssignHalfTowerSpots(state, EastHalfOrder, coords);
        return AiMove.Create(coords).NaturalOrder();
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
                coords[(int)role] = new Vector2(spot.X, Math.Clamp(spot.Y + 6.2f, -15f, 15f));
            }
            else
            {
                soakers.Add(role);
            }
        }
        for (var i = 0; i < soakers.Count && i < towerSpots.Count; i++)
            coords[(int)soakers[i]] = towerSpots[i];
    }

    private static IAiMove Trap1StackSpots(UmadP1GravenImage1State state)
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
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove Magic2SafeSpots(UmadP1GravenImage1State state)
    {
        var supportSpot = FindIceThunderSafeSpot(state, eastSide: false);
        var dpsSpot = FindIceThunderSafeSpot(state, eastSide: true);
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = UmadP1GravenImage1State.IsDps(role) ? dpsSpot : supportSpot;
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 FindIceThunderSafeSpot(UmadP1GravenImage1State state, bool eastSide)
    {
        var preferred = SafeQuadrantCenter(state.Ice2DiagonalB, eastSide);
        if (IsSafeFromIceAndThunder(preferred, state)) return preferred;

        var best = preferred;
        var bestDist = float.PositiveInfinity;
        for (var r = 3f; r <= 17f; r += 1.5f)
        {
            for (var deg = 0; deg < 360; deg += 10)
            {
                var rad = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(rad) * r, MathF.Cos(rad) * r);
                if (!IsSafeFromIceAndThunder(q, state)) continue;
                var d = Vector2.DistanceSquared(q, preferred);
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

    private static IAiMove RaidwideStackSpots()
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = role == PartyRole.MainTank ? new Vector2(0f, -4f) : new Vector2(0f, 5f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove OverdriveSpots()
    {
        var coords = new Vector2?[8];
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            coords[(int)role] = role == PartyRole.OffTank ? new Vector2(-4f, -4f) : new Vector2(0f, 5f);
        return AiMove.Create(coords).NaturalOrder();
    }
}