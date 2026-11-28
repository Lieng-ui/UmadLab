using System;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P4KefkaSays;

namespace AnoMech.Scenarios.Umad.P4Full;

public sealed class UmadP4FullAi : IScenarioAi<UmadP4FullState>
{
    public string Name => "CN PF Full P4 (国服野队/固定队)";

    private const float ConeHalfAngle = MathF.PI / 4f;
    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private const float LineBaseRotation = -MathF.PI / 4f;
    private const float ThunderEdgeInset = 1.25f;
    private const float ThunderEdgeProbe = 2f;
    private const float BlizzardNudge = 1.5f;
    private const float AntilightLocalX = 8f;
    private const float FacingStep = 0.6f;

    private static readonly Vector2[] LineAnchors =
    {
        new(24.75f, -3.54f),
        new(17.68f, -10.61f),
        new(10.61f, -17.68f),
        new(3.54f, -24.75f),
    };

    private static readonly Vector2?[] CnElementCardinals =
    {
        new(0f, -10.5f), new(0f, -10.5f), new(0f, -10.5f), new(10.5f, 0f),
        new(0f, 10.5f), new(0f, 10.5f), new(0f, 10.5f), new(-10.5f, 0f),
    };

    private float ksOffset;
    private float AtKs(float t) => ksOffset + t;

    public void Run(UmadP4FullState state, SimWorld world)
    {
        var ks = state.KefkaSays;
        ksOffset = state.CountdownSeconds + UmadP4FullScenario.PreKefkaSaysOffsetSec;
        var ai = new AiManager(world);

        ai.Move(0.1f, () => Stack(new Vector2(0f, 3.5f)), jitter: 0.6f);
        ai.Move(AtKs(2f), () => Stack(new Vector2(0f, 0f)), jitter: 1.0f);
        ai.Move(AtKs(11.3f), () => Stack(SafeSpot(ks.Mystery[0])), jitter: 0.6f, sprint: true);
        ai.Move(AtKs(26.1f), () => Stack(SafeSpot(ks.Mystery[1])), jitter: 0.6f, sprint: true);
        ai.Move(AtKs(41.2f), () => Stack(SafeSpot(ks.Mystery[2])), jitter: 0.6f, sprint: true);

        ai.Move(AtKs(57.5f), () => FloodOfNaught(ks), jitter: 1.5f, sprint: true);
        ai.Move(AtKs(63.2f), () => ResolveElements(ks.ElemRoles[0], ks.ElemTrue[0]), sprint: true);

        ai.Move(AtKs(72.0f), () => CnWave1Gaze(ks.Wave1, ks.Mystery[3], ks.Wave1True), jitter: 0f, sprint: true);
        world.Events.Add(AtKs(79.4f), () => FaceWave1GazeBots(world, ks.Wave1, ks.Mystery[3], ks.Wave1True));

        ai.Move(AtKs(81.0f), () => Stack(new Vector2(0f, 0f)), jitter: 0.6f, sprint: true);
        ai.Move(AtKs(87.6f), () => StrayFlames(ks.InfernoMystery), jitter: 0.5f, sprint: true);

        ai.Move(AtKs(92.5f), () => ResolveElementsUnderBlizzard(ks.ElemRoles[1], ks.ElemTrue[1], ks.Mystery[3]), sprint: true);

        ai.Move(AtKs(97.5f), () => CnWave2Gaze(ks.Wave2, ks.Wave2True), jitter: 0f, sprint: true);
        world.Events.Add(AtKs(103.4f), () => FaceWave2GazeBots(world, ks.Wave2, ks.Wave2True));

        ai.Move(AtKs(105.0f), () => Stack(new Vector2(0f, 0f)), jitter: 0.6f, sprint: true);
        ai.Move(AtKs(110.3f), () => StraySprayAndMystery(ks.TsunamiMystery, ks.Mystery[4]), jitter: 0.3f, sprint: true);
        ai.Move(AtKs(116.5f), () => Stack(new Vector2(0f, 0f)), jitter: 0.6f);

        ScheduleAccelerationBombDodge(ks, world);
    }

    private void ScheduleAccelerationBombDodge(UmadP4KefkaSaysState ks, SimWorld world)
    {
        var role = world.Party.PlayerRole;
        var w1 = Array.IndexOf(ks.Wave1.List, role) % 4;
        var w2 = Array.IndexOf(ks.Wave2.List, role) % 4;

        (float resolveTime, bool real)? bomb =
            w2 == 0 ? (96.28f, ks.Wave2True) :
            w2 == 1 ? (71.28f, ks.Wave2True) :
            w1 == 0 ? (71.36f, ks.Wave1True) :
            w1 == 1 ? (96.36f, ks.Wave1True) :
            null;

        if (bomb is not ({ } resolveTime, false)) return;

        world.Events.Add(AtKs(resolveTime - 0.6f), () =>
        {
            var member = world.Party.Get(role);
            if (member == null || !member.IsAlive()) return;
            var toDestination = (member.MoveDestination ?? member.Position) - member.Position;
            var step = toDestination.Length() > 0.05f ? Vector3.Normalize(toDestination) : Vector3.UnitX;
            member.MoveTo(member.Position + step * 0.3f, speed: 0.4f);
        });
    }

    private static (Vector2 Right, Vector2 Down) Wave1GazeBasis(MysteryCast thunder)
    {
        var rot = LineBaseRotation * thunder.LightningOrientation;
        var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
        Vector2 outerMid = new(0f, 1f);
        for (var i = 0; i < 4; i++)
        {
            if ((i + thunder.LightningOffset) % 2 != 0) continue;
            var anchor = new Vector2(LineAnchors[i].X * thunder.LightningOrientation, LineAnchors[i].Y);
            var mid = anchor + fwd * 20f;
            if (mid.LengthSquared() > 100f)
            {
                outerMid = mid;
                break;
            }
        }
        var down = Vector2.Normalize(outerMid);
        var right = new Vector2(down.Y, -down.X);
        return (right, down);
    }

    private static IAiMove CnWave1Gaze(RoleList wave, MysteryCast thunder, bool isTrue)
    {
        var (right, down) = Wave1GazeBasis(thunder);
        var coords = new Vector2?[8];
        int[] others = [1, 2, 3, 5, 6, 7];

        if (isTrue)
        {
            coords[(int)wave[0]] = right * -1f + down * 8f;
            coords[(int)wave[4]] = right * 1f + down * 8f;
            for (var k = 0; k < others.Length; k++)
            {
                var rx = (k % 3 - 1f) * 0.45f;
                var dy = 4f + (k / 3 - 0.5f) * 0.45f;
                coords[(int)wave[others[k]]] = right * rx + down * dy;
            }
        }
        else
        {
            coords[(int)wave[0]] = right * -4f + down * 1f;
            coords[(int)wave[4]] = right * 4f + down * 1f;
            for (var k = 0; k < others.Length; k++)
            {
                var rx = (k % 3 - 1f) * 0.45f;
                var dy = 8f + (k / 3 - 0.5f) * 0.45f;
                coords[(int)wave[others[k]]] = right * rx + down * dy;
            }
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static void FaceWave1GazeBots(SimWorld world, RoleList wave, MysteryCast thunder, bool isTrue)
    {
        var (_, down) = Wave1GazeBasis(thunder);
        var down3 = new Vector3(down.X, 0f, down.Y);
        var g0 = world.Party.Get(wave[0]);
        var g4 = world.Party.Get(wave[4]);
        for (var i = 0; i < 8; i++)
        {
            var m = world.Party.Get(i);
            if (m is null || !m.IsAlive()) continue;
            if (m is SimPlayer && !DebugBotControl.Enabled) continue;
            var role = (PartyRole)i;
            if (role == wave[0] && g4 is not null)
                m.Face(isTrue ? m.Position - (g4.Position - m.Position) : g4.Position);
            else if (role == wave[4] && g0 is not null)
                m.Face(isTrue ? m.Position - (g0.Position - m.Position) : g0.Position);
            else
                m.Face(m.Position - down3 * 5f);
        }
    }

    private static IAiMove CnWave2Gaze(RoleList wave, bool isTrue)
    {
        var coords = new Vector2?[8];
        int[] others = [1, 2, 3, 5, 6, 7];

        if (isTrue)
        {
            coords[(int)wave[0]] = new Vector2(-1f, 8f);
            coords[(int)wave[4]] = new Vector2(1f, 8f);
            for (var k = 0; k < others.Length; k++)
            {
                var x = (k % 3 - 1f) * 0.45f;
                var y = 4f + (k / 3 - 0.5f) * 0.45f;
                coords[(int)wave[others[k]]] = new Vector2(x, y);
            }
        }
        else
        {
            coords[(int)wave[0]] = new Vector2(-4f, 0f);
            coords[(int)wave[4]] = new Vector2(4f, 0f);
            for (var k = 0; k < others.Length; k++)
            {
                var x = (k % 3 - 1f) * 0.45f;
                var y = 8f + (k / 3 - 0.5f) * 0.45f;
                coords[(int)wave[others[k]]] = new Vector2(x, y);
            }
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static void FaceWave2GazeBots(SimWorld world, RoleList wave, bool isTrue)
    {
        var g0 = world.Party.Get(wave[0]);
        var g4 = world.Party.Get(wave[4]);
        for (var i = 0; i < 8; i++)
        {
            var m = world.Party.Get(i);
            if (m is null || !m.IsAlive()) continue;
            if (m is SimPlayer && !DebugBotControl.Enabled) continue;
            var role = (PartyRole)i;
            if (role == wave[0] && g4 is not null)
                m.Face(isTrue ? m.Position - (g4.Position - m.Position) : g4.Position);
            else if (role == wave[4] && g0 is not null)
                m.Face(isTrue ? m.Position - (g0.Position - m.Position) : g0.Position);
            else
                m.Face(m.Position - Vector3.UnitZ * 5f);
        }
    }

    private static IAiMove ResolveElements(RoleList roleList, bool isTrue) =>
        ElementFormation(roleList, isTrue, CnElementCardinals);

    private static IAiMove ResolveElementsUnderBlizzard(RoleList roleList, bool isTrue, MysteryCast blizzard)
    {
        var s = blizzard.BlizzardOffset == 0 ? BlizzardNudge : -BlizzardNudge;
        return ElementFormation(roleList, isTrue, CnElementCardinals, p =>
        {
            p.AddX(0, s); p.AddX(1, s); p.AddX(2, s);
            p.AddY(3, -s);
            p.AddX(4, -s); p.AddX(5, -s); p.AddX(6, -s);
            p.AddY(7, s);
        });
    }

    private static IAiMove ElementFormation(RoleList roleList, bool isTrue, Vector2?[] coords,
                                            Action<IAiPositions>? adjust = null)
    {
        var move = AiMove.Create(coords)
                         .Assignments([
                             roleList[0],
                             roleList[1],
                             isTrue ? roleList[3] : roleList[2],
                             isTrue ? roleList[2] : roleList[3],
                             roleList[4],
                             roleList[5],
                             isTrue ? roleList[7] : roleList[6],
                             isTrue ? roleList[6] : roleList[7]
                         ]);
        return adjust is null ? move : move.ApplyPositions(adjust);
    }

    private static IAiMove Stack(Vector2 spot) => AiMove.All(spot);

    private static IAiMove StrayFlames(ChaosMystery inferno)
    {
        if (!inferno.SolutionIsChariot)
            return AiMove.All(new Vector2(0f, 0f));

        const float radius = 11f;
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var th = i * MathF.PI / 4f;
            coords[i] = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove StraySprayAndMystery(ChaosMystery water, MysteryCast mystery) =>
        AiMove.All(water.SolutionIsChariot ? FarSafeSpot(mystery) : SafeSpot(mystery));

    private static Vector2 SafeSpot(MysteryCast mystery)
    {
        const float wantClearance = 1.5f;
        Vector2 best = new(0f, 0f);
        var bestClear = float.NegativeInfinity;
        for (var radius = 2f; radius <= 6f; radius += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 2)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
                var clear = Clearance(q, mystery);
                if (clear > bestClear) { bestClear = clear; best = q; }
            }
            if (bestClear >= wantClearance) break;
        }
        return best;
    }

    private static Vector2 FarSafeSpot(MysteryCast mystery)
    {
        Vector2 best = new(0f, 10f);
        var bestClear = float.NegativeInfinity;
        for (var radius = 8f; radius <= 12f; radius += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 2)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
                var clear = Clearance(q, mystery);
                if (clear > bestClear) { bestClear = clear; best = q; }
            }
        }
        return best;
    }

    private static float Clearance(Vector2 q, MysteryCast mystery)
    {
        var min = LineClearance(q, mystery);
        var r = q.Length();
        var qBearing = MathF.Atan2(q.X, q.Y);

        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.BlizzardOffset) % 2 != 0) continue;
            var coneBearing = MathF.PI / 2f * i + MathF.PI / 4f;
            var d = AngleDiff(qBearing, coneBearing);
            min = MathF.Min(min, r * MathF.Sin(d - ConeHalfAngle));
        }
        return min;
    }

    private static float LineClearance(Vector2 q, MysteryCast mystery)
    {
        var min = float.PositiveInfinity;
        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.LightningOffset) % 2 != 0) continue;
            var anchor = new Vector2(LineAnchors[i].X * mystery.LightningOrientation, LineAnchors[i].Y);
            var rot = LineBaseRotation * mystery.LightningOrientation;
            var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
            var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
            var rel = q - anchor;
            var along = Vector2.Dot(rel, fwd);
            if (along < 0f || along > LineLength) continue;
            min = MathF.Min(min, MathF.Abs(Vector2.Dot(rel, right)) - LineHalfWidth);
        }
        return min;
    }

    private static float AngleDiff(float a, float b)
    {
        var d = MathF.Abs(a - b) % (2f * MathF.PI);
        return d > MathF.PI ? 2f * MathF.PI - d : d;
    }

    private static IAiMove FloodOfNaught(UmadP4KefkaSaysState state)
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var role = state.Wave3[i];
            var rightSide = (i % 2 == 0) ^ state.Wounds[i] ^ (state.Antilights[0].Antilight == Antilight.White);
            var local = new Vector3(rightSide ? -AntilightLocalX : AntilightLocalX, 0f, 0f);
            var world = state.NeoExdeathDirection.Apply(local);
            coords[(int)role] = new Vector2(world.X, world.Z);
        }
        return AiMove.Create(coords).NaturalOrder();
    }
}
