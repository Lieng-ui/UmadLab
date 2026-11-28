using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios.Umad.P3BlackHole;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3Full;

internal sealed class UmadP3FullBlackHoleAi
{
    private static int TetherSeat(int seat) => seat;

    private UmadP3BlackHoleState state = null!;
    private SimWorld world = null!;
    private PartyRole[] priorityRoles = null!;

    private float timeOffset;
    private float At(float t) => timeOffset + t;

    public void RunAt(UmadP3BlackHoleState stateParam, SimWorld worldParam, float timeOffsetParam, PartyRole[]? prioritySeats = null)
    {
        timeOffset = timeOffsetParam;
        state = stateParam;
        world = worldParam;
        priorityRoles = prioritySeats is { Length: 8 } ? prioritySeats : stateParam.Roles.List.ToArray();
        assignedHole.Clear();
        setHoles.Clear();
        implosionChaos = null;
        implosionSpots = null;
        var ai = new AiManager(world);

        world.Events.Add(At(25.17f), () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[0]);
        world.Events.Add(At(55.70f), () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[1]);
        world.Events.Add(At(89.95f), () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[2]);
        world.Events.Add(At(123.83f), () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[3]);

        ai.Automarker(At(8.80f), BuildCnBlackHoleMarkers);
        world.Events.Add(At(143.7f), () => world.Party.ClearMarks());
        ai.Move(At(5.0f), StackCentreTanksHoldBossesCentred, sprint: true);
        ai.Move(At(9.5f), StackCentreTanksHoldBossesCentred);
        ai.Move(At(13.0f), StackCentre);

        ai.Move(At(18f), () => DodgeSlap(slapIndex: 0, kefkaIndex: 0));

        ai.Move(At(25f), StackCentre);
        world.Events.Add(At(26.2f), () => GrabNewTether(tetherIndex: 0, playerIndex: 4));
        ScheduleTetherRegrab(At(26.6f), At(28.2f), tetherIndex: 0, playerIndex: 4);
        world.Events.Add(At(28.2f), () => PullTether(playerIndex: 4));
        world.Events.Add(At(32.4f), () => GrabNewTether(tetherIndex: 0, playerIndex: 4));
        world.Events.Add(At(32.4f), () => GrabNewTether(tetherIndex: 1, playerIndex: 0));
        world.Events.Add(At(34.4f), () => PullTether(playerIndex: 4));
        world.Events.Add(At(34.4f), () => PullTether(playerIndex: 0));

        ai.Move(At(37.6f), PullExdeathOutForThunder, sprint: true);
        ai.Move(At(39.45f), MoveMtClearOfFirstThunder, sprint: true);
        world.Events.Add(At(39.4f), ResolveFirstThunder);
        ai.Move(At(43.19f), SwapFirstThunderTanks);

        {
            var (set1First, set1Second) = ThunderIIIPlanning.Roles(state.ThunderSet1);
            ScheduleThunderClearance(At(39.5f), At(43.19f), At(43.5f), At(46.2f), set1First, set1Second);
        }
        ai.Move(At(46.5f), DodgeEdict);

        ai.Move(At(50.6f), () => DodgeSlap(slapIndex: 1, kefkaIndex: 1), sprint: true);
        ai.Move(At(56f), StackCentre);
        world.Events.Add(At(58.1f), () => GrabNewTether(tetherIndex: 0, playerIndex: 4));
        world.Events.Add(At(58.1f), () => GrabNewTether(tetherIndex: 1, playerIndex: 0));
        world.Events.Add(At(58.1f), () => GrabNewTether(tetherIndex: 2, playerIndex: 3));
        world.Events.Add(At(60.1f), () => PullTether(playerIndex: 4));
        world.Events.Add(At(60.1f), () => PullTether(playerIndex: 0));
        world.Events.Add(At(60.1f), () => PullTether(playerIndex: 3));
        world.Events.Add(At(64f), () => GrabHandoffTether(tetherIndex: 0, playerIndex: 5, intercept: 2.5f));
        world.Events.Add(At(65.5f), () => ReturnToMiddleWhenFree(playerIndex: 4));
        world.Events.Add(At(66.2f), () => PullTether(playerIndex: 5));
        world.Events.Add(At(69f), () => GrabHandoffTether(tetherIndex: 1, playerIndex: 1, intercept: 2.5f));
        world.Events.Add(At(70.5f), () => ReturnToMiddleWhenFree(playerIndex: 0));
        world.Events.Add(At(71.2f), () => PullTether(playerIndex: 1));
        ai.Move(At(73.1f), DodgeEdictAndLookUpon, sprint: true);
        ai.Move(At(79.6f), PullExdeathForSecondThunder, sprint: true);
        world.Events.Add(At(81.5f), ResolveSecondThunder);
        ai.Move(At(84.2f), SwapSecondThunderTanksKeepChaosCentered, sprint: true);

        {
            var (set2First, set2Second) = ThunderIIIPlanning.Roles(state.ThunderSet2);
            ScheduleThunderClearance(At(80.5f), At(84.2f), At(84.6f), At(87.5f), set2First, set2Second);
        }
        ai.Move(At(88f), StackCentre);
        world.Events.Add(At(92.3f), () => GrabNewTether(tetherIndex: 0, playerIndex: 5));
        world.Events.Add(At(92.3f), () => GrabNewTether(tetherIndex: 1, playerIndex: 1));
        world.Events.Add(At(92.3f), () => GrabNewTether(tetherIndex: 2, playerIndex: 7));
        world.Events.Add(At(94.3f), () => PullTether(playerIndex: 5));
        world.Events.Add(At(94.3f), () => PullTether(playerIndex: 1));
        world.Events.Add(At(94.3f), () => PullTether(playerIndex: 7));
        world.Events.Add(At(98f), () => GrabHandoffTether(tetherIndex: 0, playerIndex: 6, intercept: 2.5f));
        world.Events.Add(At(99.5f), () => ReturnToMiddleWhenFree(playerIndex: 5));
        world.Events.Add(At(100.2f), () => PullTether(playerIndex: 6));
        world.Events.Add(At(103f), () => GrabHandoffTether(tetherIndex: 1, playerIndex: 2, intercept: 2.5f));
        world.Events.Add(At(104.5f), () => ReturnToMiddleWhenFree(playerIndex: 1));
        world.Events.Add(At(105.2f), () => PullTether(playerIndex: 2));

        world.Events.Add(At(109.5f), () => ReturnToMiddle(playerIndex: 7));
        world.Events.Add(At(110.5f), () => ReturnToMiddle(playerIndex: 6));
        world.Events.Add(At(111.5f), () => ReturnToMiddle(playerIndex: 2));
        world.Events.Add(At(110f), () => AnchorMtForImplosion(kefkaIndex: 3));

        ai.Move(At(117f), () => DodgeImplosion(shockwaveIndex: 0, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f, arrivalTime: At(119.09f));
        ai.Move(At(119.2f), () => DodgeImplosion(shockwaveIndex: 1, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f, arrivalTime: At(121.11f));
        ai.Move(At(121.3f), () => DodgeSlap(slapIndex: 2, kefkaIndex: 3), sprint: true);
        ai.Move(At(124f), StackCentre);
        world.Events.Add(At(125.9f), () => GrabNewTether(tetherIndex: 0, playerIndex: 6));
        world.Events.Add(At(125.9f), () => GrabNewTether(tetherIndex: 1, playerIndex: 2));
        world.Events.Add(At(127.9f), () => PullTether(playerIndex: 6));
        world.Events.Add(At(127.9f), () => PullTether(playerIndex: 2));
        world.Events.Add(At(130.8f), () => GrabTether(tetherIndex: 0, playerIndex: 2));
        world.Events.Add(At(130.8f), () => ReturnToMiddle(playerIndex: 6));
        ScheduleTetherRegrab(At(131.2f), At(134f), tetherIndex: 0, playerIndex: 2);
        ai.Move(At(134f), () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4, leaveWithoutTether: false), sprint: true);
        ai.Move(At(134f), () => DodgeLookUponSplitOthers(tetherPlayerIndex: 2, lookKefkaIndex: 4), sprint: true);
        ai.Move(At(134.5f), () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4, leaveWithoutTether: false), sprint: true);
        ai.Move(At(135f), () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4, leaveWithoutTether: true), sprint: true);
        ai.Move(At(139f), PrepositionForStomp);
        ai.Move(At(147f), StompBlizzardCorners);
        ai.Move(At(149.8f), StompStackAndTowers);
        ai.Move(At(152.4f), StompSwapWest);
        ai.Move(At(153.7f), StompSwapEast);
        ai.Move(At(158.0f), SpreadOutForBigBangAndBlizzard, sprint: true);
        ai.Move(At(160.2f), KeepMovingForBlizzard);
        ai.Move(At(163.0f), StackCentre);
    }

    
    private Dictionary<PartyRole, Sign> BuildCnBlackHoleMarkers() => new()
    {
        [priorityRoles[4]] = Sign.Attack1,
        [priorityRoles[0]] = Sign.Attack2,
        [priorityRoles[3]] = Sign.Attack3,
        [priorityRoles[5]] = Sign.Bind1,
        [priorityRoles[1]] = Sign.Bind2,
        [priorityRoles[7]] = Sign.Bind3,
        [priorityRoles[6]] = Sign.Ignore1,
        [priorityRoles[2]] = Sign.Ignore2,
    };

    private IAiMove StackCentreTanksHoldBossesCentred()
    {
        var mtSpot = BossCenterPullSpot(state.ScenarioObjects.Chaos, world.Party.Get(PartyRole.MainTank), 6.0f);
        var stSpot = BossCenterPullSpot(state.ScenarioObjects.Exdeath, world.Party.Get(PartyRole.OffTank), 3.8f);
        return AiMove.Create(
            mtSpot,
            stSpot,
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f)).NaturalOrder();
    }

    private static Vector2 BossCenterPullSpot(SimCharacter? boss, SimCharacter? tank, float fallbackStopDist)
    {
        if (boss is null || !boss.IsAlive()) return Vector2.Zero;
        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        if (bossPos.LengthSquared() <= 0.64f) return Vector2.Zero;
        var stopDist = boss.HitboxRadius + (tank?.HitboxRadius ?? 0f);
        if (stopDist < 1.0f) stopDist = fallbackStopDist;
        stopDist = MathF.Min(stopDist, ArenaRadius - 2f);
        return -Vector2.Normalize(bossPos) * stopDist;
    }

    private static IAiMove StackCentre() => AiMove.All(new(0f, 0f));

    private IAiMove StackCentreAndRecenterChaos()
    {
        var mtSpot = BossCenterPullSpot(state.ScenarioObjects.Chaos, world.Party.Get(PartyRole.MainTank), 6.0f);
        mtSpot = world.Obstacles.ClampOutside(mtSpot, margin: 1.5f);
        return AiMove.Create(
            mtSpot,
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f)).NaturalOrder();
    }

    private IAiMove DodgeSlap(int slapIndex, int kefkaIndex)
    {
        var move = SlapSpots(slapIndex, kefkaIndex);
        for (int i = 0; i < 8; i++)
        {
            var member = world.Party.Get(i);
            if (member is null || !member.IsAlive()) continue;
            AnoMech.Core.DiagnosticLog.Info(
                $"[UmadP3BlackHoleAi] DodgeSlap({slapIndex},{kefkaIndex}): role{i} from ({member.Position.X:F1},{member.Position.Z:F1}) -> target {move[i]}.");
        }
        return move;
    }

    private IAiMove SlapSpots(int slapIndex, int kefkaIndex)
    {
        var direction = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Right
                            ? state.KefkaPosition[kefkaIndex].Flip()
                            : state.KefkaPosition[kefkaIndex];

        IAiMove move = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left
                   ? (IAiMove)AiMove.Create(
                                        new(7.4f, 0f), new(7.6f, 0f),
                                        new(7.8f, 0f), new(7.8f, 0f),
                                        new(7.8f, 0f), new(7.8f, 0f), new(7.8f, 0f), new(7.8f, 0f))
                                    .NaturalOrder()
                                    .ApplyPositions(direction.Apply)
                   : (IAiMove)AiMove.Create(
                                        new(6.2f, 4.6f), new(6.4f, 4.8f),
                                        new(7.6f, 0f), new(7.6f, 0f),
                                        new(6.2f, -4.6f), new(6.2f, -4.6f), new(6.2f, -4.6f), new(6.2f, -4.6f))
                                    .NaturalOrder()
                                    .ApplyPositions(direction.Apply);
        return move;
    }

    private const float ThunderBusterRadius = 8f;
    private const float ThunderClearDistance = 10f;

    private IAiMove PullExdeathOutForThunder()
    {
        var exPos = state.ScenarioObjects.Exdeath is { } ex
            ? new Vector2(ex.Position.X, ex.Position.Z)
            : new Vector2(0f, 6f);
        var outDir = exPos.LengthSquared() > 1f ? Vector2.Normalize(exPos) : new Vector2(0f, 1f);
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = outDir * 14.5f;
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove MoveMtClearOfFirstThunder()
    {
        var exPos = state.ScenarioObjects.Exdeath is { } ex
            ? new Vector2(ex.Position.X, ex.Position.Z)
            : new Vector2(0f, 6f);
        var outDir = exPos.LengthSquared() > 1f ? Vector2.Normalize(exPos) : new Vector2(0f, 1f);
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = -outDir * 3.5f;
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove PullExdeathForSecondThunder()
    {
        var exPos = state.ScenarioObjects.Exdeath is { } ex
            ? new Vector2(ex.Position.X, ex.Position.Z)
            : new Vector2(0f, 6f);
        var outDir = exPos.LengthSquared() > 1f ? Vector2.Normalize(exPos) : new Vector2(0f, 1f);
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = outDir * 11.2f;
        coords[(int)PartyRole.MainTank] = -outDir * 3.0f;
        for (int i = 2; i < 8; i++)
            coords[i] = -outDir * 5.0f;
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove SwapSecondThunderTanksKeepChaosCentered()
    {
        var (first, second) = ThunderIIIPlanning.Roles(state.ThunderSet2);
        if (second is not { } secondRole) return AiMove.Create().NaturalOrder();
        if (first != PartyRole.OffTank || secondRole != PartyRole.MainTank)
            return SwapSecondThunderTanks();

        var chaosPos = state.ScenarioObjects.Chaos is { } ch
            ? new Vector2(ch.Position.X, ch.Position.Z)
            : Vector2.Zero;
        var exPos = state.ScenarioObjects.Exdeath is { } ex
            ? new Vector2(ex.Position.X, ex.Position.Z)
            : new Vector2(0f, 7.4f);
        var diff = exPos - chaosPos;
        var dir = diff.LengthSquared() > 0.25f ? Vector2.Normalize(diff) : new Vector2(0f, 1f);
        var mt = world.Party.Get(PartyRole.MainTank);
        var chaosStop = (state.ScenarioObjects.Chaos?.HitboxRadius ?? 0f) + (mt?.HitboxRadius ?? 0f);
        if (chaosStop < 1.0f) chaosStop = 6.0f;
        var mtDist = MathF.Min(chaosStop - 0.35f, diff.Length());
        var mtSpot = chaosPos + dir * mtDist;
        var stSpot = exPos + dir * 10.5f;
        if (stSpot.Length() > ArenaRadius - 1f)
            stSpot = Vector2.Normalize(stSpot) * (ArenaRadius - 1f);

        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = mtSpot;
        coords[(int)PartyRole.OffTank] = stSpot;
        for (int i = 2; i < 8; i++)
            coords[i] = chaosPos - dir * 5.2f;
        return AiMove.Create(coords).NaturalOrder();
    }

    private void ResolveFirstThunder()
    {
        if (state.ScenarioObjects.Exdeath is not { } exdeath) return;
        var (first, _) = ThunderIIIPlanning.Roles(state.ThunderSet1);
        if (world.Party.Get((int)first) is not { } firstMember || !firstMember.IsAlive()) return;
        (firstMember as ISimPartyMember)?.UseSprint(3.5f);
        firstMember.Follow(exdeath, speed: AiManager.SprintSpeed);
    }

    private void ResolveSecondThunder()
    {
        if (state.ScenarioObjects.Exdeath is not { } exdeath) return;
        var (first, _) = ThunderIIIPlanning.Roles(state.ThunderSet2);
        if (world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive())
            firstMember.Follow(exdeath);
    }

    private IAiMove SwapThunderTanks(ThunderIIIAssignment plan)
    {
        var (first, second) = ThunderIIIPlanning.Roles(plan);
        if (second is not { } secondRole) return AiMove.Create().NaturalOrder();
        var a = world.Party.Get(first);
        var b = world.Party.Get(secondRole);
        if (a is null || b is null) return AiMove.Create().NaturalOrder();

        var coords = new Vector2?[8];
        coords[(int)secondRole] = new Vector2(a.Position.X, a.Position.Z);
        coords[(int)first]      = new Vector2(b.Position.X, b.Position.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove SwapFirstThunderTanks() => SwapThunderTanks(state.ThunderSet1);
    private IAiMove SwapSecondThunderTanks() => SwapThunderTanks(state.ThunderSet2);

    private void EnforceThunderClearanceOnce(PartyRole first, PartyRole? second)
    {

        if (second is { } secondRole
            && world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive()
            && world.Party.Get((int)secondRole) is { } secondMember && secondMember.IsAlive())
        {
            var firstPos = new Vector2(firstMember.Position.X, firstMember.Position.Z);
            var secondPos = new Vector2(secondMember.Position.X, secondMember.Position.Z);
            var offset = secondPos - firstPos;
            if (offset.LengthSquared() < ThunderClearDistance * ThunderClearDistance)
            {
                var away = firstPos.LengthSquared() > 1e-4f ? Vector2.Normalize(-firstPos) : new Vector2(0f, -1f);
                var seat = firstPos + RotateVec(away, MathF.PI / 2f) * ThunderClearDistance;

                var seatClearRadius = ArenaRadius - 1f;
                if (seat.LengthSquared() > seatClearRadius * seatClearRadius)
                    seat = seat.LengthSquared() > 1e-4f ? Vector2.Normalize(seat) * seatClearRadius : seat;
                secondMember.MoveTo(new Vector3(seat.X, 0f, seat.Y));
            }
        }

        if (state.ScenarioObjects.Exdeath is { } exdeath && exdeath.IsAlive())
        {
            var bossPos = new Vector2(exdeath.Position.X, exdeath.Position.Z);
            for (int i = 0; i < 8; i++)
            {
                var role = (PartyRole)i;
                if (role == first || (second is { } sec && role == sec)) continue;
                if (world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
                var pos = new Vector2(member.Position.X, member.Position.Z);
                var offset = pos - bossPos;
                if (offset.LengthSquared() >= ThunderClearDistance * ThunderClearDistance) continue;
                var dir = role == PartyRole.MainTank && bossPos.LengthSquared() > 1f
                    ? Vector2.Normalize(-bossPos)
                    : (offset.LengthSquared() > 1e-4f ? Vector2.Normalize(offset) : new Vector2(1f, 0f));
                var target = bossPos + dir * ThunderClearDistance;

                var clearRadius = ArenaRadius - 1f;
                if (target.LengthSquared() > clearRadius * clearRadius)
                    target = target.LengthSquared() > 1e-4f ? Vector2.Normalize(target) * clearRadius : target;
                member.MoveTo(new Vector3(target.X, 0f, target.Y));
            }
        }
    }

    private const float ThunderClearanceInterval = 0.4f;

    private void ScheduleThunderClearance(float fromTime, float swapStart, float swapLand, float toTime, PartyRole first, PartyRole? second)
    {
        for (var t = fromTime; t < swapStart; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(first, second));
        var postSwapExcluded = second ?? first;
        for (var t = swapLand; t <= toTime; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(postSwapExcluded, null));
    }

    private IAiMove DodgeEdict()
    {
        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return StackCentre();

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        const float margin = 3f;

        var s = margin - Vector2.Dot(bossPos, fwd);
        var target = s > 0f ? -fwd * s : Vector2.Zero;
        AnoMech.Core.DiagnosticLog.Info(
            $"[UmadP3BlackHoleAi] DodgeEdict: boss at ({bossPos.X:F1},{bossPos.Y:F1}) rot={boss.Rotation:F3} -> target ({target.X:F1},{target.Y:F1}).");
        return AiMove.All(target);
    }

    private IAiMove DodgeEdictAndLookUpon()
    {
        var theta = state.KefkaPosition[2].RadiansFromNorth;
        var axis = new Vector2(-MathF.Sin(theta), MathF.Cos(theta));
        var lookRight = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        const float lookSafe = 11f;
        const float behind = 3f;

        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return AiMove.All(lookRight * lookSafe);

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        var denom = Vector2.Dot(axis, fwd);

        float Fwd(Vector2 p) => Vector2.Dot(p - bossPos, fwd);

        Vector2 Seat(float sign)
        {
            var edge = lookRight * (sign * lookSafe);
            var f = Fwd(edge);
            if (f <= -behind || MathF.Abs(denom) < 0.1f) return edge;
            var maxSlide = MathF.Sqrt((ArenaRadius - 1f) * (ArenaRadius - 1f) - lookSafe * lookSafe);
            var t = Math.Clamp((-behind - f) / denom, -maxSlide, maxSlide);
            return edge + axis * t;
        }

        var a = Seat(1f);
        var b = Seat(-1f);
        bool aOk = Fwd(a) < -1f, bOk = Fwd(b) < -1f;
        if (aOk == bOk) return AiMove.All(a.LengthSquared() <= b.LengthSquared() ? a : b);
        return AiMove.All(aOk ? a : b);
    }

    private const float SideTieTolerance = 1e-3f;
    private const float ArenaRadius = 20f;
    private const float ImplosionBorderInset = 1f;
    private const float ImplosionMinChaosDistance = 4f;
    private const float ImplosionConeLean = 0.18f;
    private const float SlapRadius = 13f;
    private const float SlapClearance = 1.5f;

    private (Vector2 Centre, float Rotation)? implosionChaos;
    private (int Diagonal, float Distance)[]? implosionSpots;

    private IAiMove DodgeImplosion(int shockwaveIndex, int slapIndex, int slapKefkaIndex)
    {
        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return StackCentre();

        if (shockwaveIndex == 0 || implosionChaos is null)
            implosionChaos = (new Vector2(boss.Position.X, boss.Position.Z), boss.Rotation);
        var (c, rotation) = implosionChaos.Value;
        var baseAxis = rotation + (state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f);

        if (shockwaveIndex == 0 || implosionSpots is null)
            implosionSpots = PickImplosionSpots(c, baseAxis, slapIndex, slapKefkaIndex);

        var coneAxisAngle = baseAxis + shockwaveIndex * (MathF.PI / 2f);
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var (diagonal, distance) = implosionSpots[i];
            coords[i] = ImplosionSpot(c, baseAxis, coneAxisAngle, diagonal, distance);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private (int Diagonal, float Distance)[] PickImplosionSpots(Vector2 c, float baseAxis, int slapIndex, int slapKefkaIndex)
    {
        var slapSpots = SlapSpots(slapIndex, slapKefkaIndex);
        var slapCentres = SlapCentres(slapIndex, slapKefkaIndex);
        var spots = new (int, float)[8];
        for (var i = 0; i < 8; i++)
        {
            var goal = slapSpots[i] ?? Vector2.Zero;
            var bestCost = float.MaxValue;
            for (var k = 0; k < 4; k++)
            {
                var d = Diagonal(baseAxis, k);
                var maxDistance = MathF.Min(RayToArenaBorder(c, RotateVec(d, ImplosionConeLean)),
                                            RayToArenaBorder(c, RotateVec(d, -ImplosionConeLean))) - ImplosionBorderInset;
                for (var t = ImplosionMinChaosDistance; t <= maxDistance; t += 0.25f)
                {
                    var first = ImplosionSpot(c, baseAxis, baseAxis, k, t);
                    var second = ImplosionSpot(c, baseAxis, baseAxis + MathF.PI / 2f, k, t);
                    var clearance = MathF.Min(SlapClearanceAt(first, slapCentres), SlapClearanceAt(second, slapCentres));
                    var cost = Vector2.Distance(second, goal) + MathF.Max(0f, -clearance) * 100f;
                    if (cost < bestCost) { bestCost = cost; spots[i] = (k, t); }
                }
            }
        }
        return spots;
    }

    private static Vector2 Diagonal(float baseAxis, int k)
    {
        var theta = baseAxis + MathF.PI / 4f + k * (MathF.PI / 2f);
        return new Vector2(MathF.Sin(theta), MathF.Cos(theta));
    }

    private static Vector2 ImplosionSpot(Vector2 c, float baseAxis, float coneAxisAngle, int diagonal, float distance)
    {
        var dir = Diagonal(baseAxis, diagonal);
        var coneAxis = new Vector2(MathF.Sin(coneAxisAngle), MathF.Cos(coneAxisAngle));
        var perp = RotateVec(coneAxis, MathF.PI / 2f);
        if (Vector2.Dot(perp, dir) < 0f) perp = -perp;
        var lean = MathF.Sign(dir.X * perp.Y - dir.Y * perp.X) * ImplosionConeLean;
        return c + RotateVec(dir, lean) * distance;
    }

    private Vector2[] SlapCentres(int slapIndex, int kefkaIndex)
    {
        var mul = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left ? -1f : 1f;
        return Enumerable.Range(0, 3)
                         .Select(row => state.KefkaPosition[kefkaIndex].Apply(new Vector3(10f * mul, 0f, -10f + row * 10f)))
                         .Select(p => new Vector2(p.X, p.Z))
                         .ToArray();
    }

    private static float SlapClearanceAt(Vector2 p, Vector2[] slapCentres) =>
        slapCentres.Min(centre => Vector2.Distance(p, centre)) - SlapRadius - SlapClearance;

    private static float RayToArenaBorder(Vector2 from, Vector2 dir)
    {
        var proj = Vector2.Dot(from, dir);
        return -proj + MathF.Sqrt(proj * proj + ArenaRadius * ArenaRadius - from.LengthSquared());
    }

    private static Vector2 RotateVec(Vector2 v, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private void AnchorMtObliqueToKefka(int kefkaIndex)
    {
        var mt = world.Party.Get(PartyRole.MainTank);
        if (mt is null) return;
        var spot = state.KefkaPosition[kefkaIndex].Rotate(1).Apply(new Vector3(0f, 0f, -4.2f));
        mt.MoveTo(spot);
    }

    private void AnchorMtForImplosion(int kefkaIndex)
    {
        var mt = world.Party.Get(PartyRole.MainTank);
        if (mt is null) return;
        var spot = state.KefkaPosition[kefkaIndex].Rotate(1).Apply(new Vector3(0f, 0f, -4.2f));
        mt.MoveTo(spot);
    }

    private void ReturnToMiddle(int playerIndex) =>
        world.Party.Get(priorityRoles[TetherSeat(playerIndex)])?.MoveTo(new Vector3(0f, 0f, 0f));

    private void ReturnToMiddleWhenFree(int playerIndex, int retriesLeft = 8)
    {
        var player = world.Party.Get(priorityRoles[TetherSeat(playerIndex)]);
        if (player is null || !player.IsAlive()) return;
        if (TetherHeldBy(player) is not null)
        {
            if (retriesLeft > 0)
                world.Events.Add(0.3f, () => ReturnToMiddleWhenFree(playerIndex, retriesLeft - 1));
            return;
        }
        (player as ISimPartyMember)?.UseSprint(3.0f);
        player.MoveTo(new Vector3(0f, 0f, 0f));
    }

    private Vector3 LookUponHolderSpot(int holderSeat, int lookKefkaIndex)
    {
        var theta = state.KefkaPosition[lookKefkaIndex].RadiansFromNorth;

        var holder = world.Party.Get(priorityRoles[holderSeat]);
        var blackHole = (TetherHeldBy(holder) ?? state.ScenarioObjects.Tethers.FirstOrDefault())?.A;
        var alpha = blackHole is { } bh
                        ? MathF.Atan2(bh.Position.X, -bh.Position.Z)
                        : theta + MathF.PI / 2f;

        var plus = alpha + MathF.PI / 4f;
        var minus = alpha - MathF.PI / 4f;
        var beta = MathF.Abs(MathF.Sin(minus - theta)) > MathF.Abs(MathF.Sin(plus - theta)) + SideTieTolerance ? minus : plus;

        const float edge = 18f;
        return new Vector3(edge * MathF.Sin(beta), 0f, -edge * MathF.Cos(beta));
    }

    private IAiMove DodgeLookUponSplitHolder(int tetherPlayerIndex, int lookKefkaIndex, bool leaveWithoutTether)
    {
        var holderSeat = TetherSeat(tetherPlayerIndex);
        var coords = new Vector2?[8];
        if (!leaveWithoutTether && TetherHeldBy(world.Party.Get(priorityRoles[holderSeat])) is null)
            return AiMove.Create(coords).NaturalOrder();
        var holderSpot = LookUponHolderSpot(holderSeat, lookKefkaIndex);
        var holderRole = priorityRoles[holderSeat];
        coords[(int)holderRole] = new Vector2(holderSpot.X, holderSpot.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove DodgeLookUponSplitOthers(int tetherPlayerIndex, int lookKefkaIndex)
    {
        var holderSeat = TetherSeat(tetherPlayerIndex);
        var holderSpot = LookUponHolderSpot(holderSeat, lookKefkaIndex);
        var holderRole = priorityRoles[holderSeat];
        var coords = new Vector2?[8];
        for (int i = 0; i < 8; i++)
            if ((PartyRole)i != holderRole)
                coords[i] = new Vector2(-holderSpot.X, -holderSpot.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private readonly Dictionary<int, SimCharacter?> assignedHole = new();

    private readonly Dictionary<SimCharacter, float> setHoles = new();

    private const float NewSetPollSeconds = 0.05f;
    private const float NewSetMaxWaitSeconds = 4f;

    private IReadOnlyList<SimTether> SortedTethers()
    {
        var north = state.ScenarioObjects.TetherSortFrom.RadiansFromNorth;
        return state.ScenarioObjects.Tethers
                    .Where(t => t.A is not null)
                    .OrderBy(t => ClockwiseFromInclusive(north, t.A!.Position))
                    .ToList();
    }

    private static float ClockwiseFromInclusive(float north, Vector3 p)
    {
        var d = (MathF.Atan2(p.X, -p.Z) - north) % MathF.Tau;
        if (d < -0.05f) d += MathF.Tau;
        if (d >= MathF.Tau - 0.05f) d = 0f;
        return d < 0f ? 0f : d;
    }

    private void GrabNewTether(int tetherIndex, int playerIndex) => GrabNewTether(tetherIndex, playerIndex, world.Events.Elapsed, 0f);

    private void GrabNewTether(int tetherIndex, int playerIndex, float dueAt, float waited)
    {
        var tethers = SortedTethers();
        var shown = tetherIndex < tethers.Count
                    && !tethers.Any(t => t.A is { } hole && setHoles.TryGetValue(hole, out var at) && at < dueAt);
        if (!shown && waited < NewSetMaxWaitSeconds)
        {
            world.Events.Add(NewSetPollSeconds, () => GrabNewTether(tetherIndex, playerIndex, dueAt, waited + NewSetPollSeconds));
            return;
        }
        if (waited > 0f)
            AnoMech.Core.DiagnosticLog.Info($"[UmadP3BlackHoleAi] GrabTether: the new set showed {waited:F2}s after the grab was due{(shown ? "" : " -- gave up waiting")}.");
        if (GrabTether(tetherIndex, playerIndex)?.A is not { } hole) return;
        setHoles.TryAdd(hole, dueAt);
        world.Events.Add(TetherRegrabInterval, () => HoldOnTo(hole, playerIndex, dueAt + 4.2f, 3f));
    }

    private void GrabHandoffTether(int tetherIndex, int playerIndex, float intercept)
    {
        if (GrabTether(tetherIndex, playerIndex, intercept)?.A is not { } hole) return;
        var until = world.Events.Elapsed + 3.5f;
        world.Events.Add(TetherRegrabInterval, () => HoldOnTo(hole, playerIndex, until, intercept));
    }

    private void HoldOnTo(SimCharacter hole, int playerIndex, float until, float intercept = 3f)
    {
        var seat = TetherSeat(playerIndex);
        var player = world.Party.Get(priorityRoles[seat]);
        var tether = SortedTethers().FirstOrDefault(t => ReferenceEquals(t.A, hole));
        if (player is null || !player.IsAlive() || tether is null || world.Events.Elapsed >= until) return;
        if (!ReferenceEquals(assignedHole.GetValueOrDefault(seat), hole)) return;
        if (!ReferenceEquals(tether.B, player) && !player.IsIntercepting)
        {
            var role = (player as ISimPartyMember)?.Role.ToString() ?? $"player#{seat}";
            AnoMech.Core.DiagnosticLog.Warn($"[UmadP3BlackHoleAi] HoldOnTo: {role} lost assigned tether at t={world.Events.Elapsed:F2}s -- re-intercepting.");
            (player as ISimPartyMember)?.UseSprint(2.0f);
            player.Intercept(tether, intercept);
            world.Events.Add(0.6f, () => PullTether(playerIndex, retriesLeft: 2));
        }
        world.Events.Add(TetherRegrabInterval, () => HoldOnTo(hole, playerIndex, until, intercept));
    }

    private SimTether? GrabTether(int tetherIndex, int playerIndex, float intercept = 3f)
    {
        var seat = TetherSeat(playerIndex);
        var player = world.Party.Get(priorityRoles[seat]);
        var tethers = SortedTethers();
        var tether = tethers.ElementAtOrDefault(tetherIndex);
        assignedHole[seat] = tether?.A;
        var role = (player as ISimPartyMember)?.Role.ToString() ?? $"player#{seat}";
        if (tether is null)
        {
            AnoMech.Core.DiagnosticLog.Warn($"[UmadP3BlackHoleAi] GrabTether: tetherIndex {tetherIndex} not found in SortedTethers ({tethers.Count} known) -- {role} sent nowhere.");
            return null;
        }
        var bh = tether.A is { } a ? new Vector2(a.Position.X, a.Position.Z) : Vector2.Zero;
        var from = player is null ? "(no player)" : $"({player.Position.X:F1},{player.Position.Z:F1})";
        AnoMech.Core.DiagnosticLog.Info($"[UmadP3BlackHoleAi] GrabTether: {role} intercepting tetherIndex {tetherIndex} (black hole at ({bh.X:F1},{bh.Y:F1})) from {from}, margin={intercept}.");
        (player as ISimPartyMember)?.UseSprint(3.0f);
        player?.Intercept(tether, intercept);
        return tether;
    }

    private const int PullTetherMaxRetries = 8;

    private void PullTether(int playerIndex, int retriesLeft = PullTetherMaxRetries)
    {
        var seat = TetherSeat(playerIndex);
        var player = world.Party.Get(priorityRoles[seat]);
        var role = (player as ISimPartyMember)?.Role.ToString() ?? $"player#{seat}";

        var hole = assignedHole.GetValueOrDefault(seat);
        var assigned = hole is null ? null : SortedTethers().FirstOrDefault(t => ReferenceEquals(t.A, hole));
        if (assigned is not { A: { } blackHole, B: { } held } || !ReferenceEquals(held, player))
        {
            AnoMech.Core.DiagnosticLog.Warn($"[UmadP3BlackHoleAi] PullTether: {role} does not hold their assigned tether yet -- pull skipped (still mid-Intercept, grab never happened, or a different hole's random seed briefly gave them someone else's). Current pos ({player?.Position.X:F1},{player?.Position.Z:F1}). Retries left: {retriesLeft}.");
            if (retriesLeft > 0)
                world.Events.Add(0.5f, () => PullTether(playerIndex, retriesLeft - 1));
            return;
        }
        var bhPos = new Vector2(blackHole.Position.X, blackHole.Position.Z);
        var heldPos = new Vector2(held.Position.X, held.Position.Z);

        var isMelee = held is ISimPartyMember pm && (pm.Role.IsTank() || pm.Role is PartyRole.MeleeDpsA or PartyRole.MeleeDpsB);
        var spot = PickSafeTetherPullSpot(bhPos, isMelee);
        AnoMech.Core.DiagnosticLog.Info($"[UmadP3BlackHoleAi] PullTether: {role} held at ({heldPos.X:F1},{heldPos.Y:F1}), black hole at ({bhPos.X:F1},{bhPos.Y:F1}) -- moving to ({spot.X:F1},{spot.Y:F1}) (dist {Vector2.Distance(heldPos, spot):F1}y).");
        player?.MoveTo(new Vector3(spot.X, 0f, spot.Y));
    }

    private const float TetherRegrabInterval = 0.4f;

    private void ScheduleTetherRegrab(float fromTime, float toTime, int tetherIndex, int playerIndex)
    {
        for (var t = fromTime; t < toTime; t += TetherRegrabInterval)
            world.Events.Add(t, () => RegrabTetherIfLost(tetherIndex, playerIndex));
    }

    private void RegrabTetherIfLost(int tetherIndex, int playerIndex)
    {
        var player = world.Party.Get(priorityRoles[TetherSeat(playerIndex)]);
        if (player is null || player.IsIntercepting || TetherHeldBy(player) is not null) return;
        GrabTether(tetherIndex, playerIndex);
    }

    private SimTether? TetherHeldBy(SimCharacter? player) =>
        player is null
            ? null
            : state.ScenarioObjects.Tethers.FirstOrDefault(t => ReferenceEquals(t.B, player));

    private Vector2 PickSafeTetherPullSpot(Vector2 bhPos, bool isMelee)
    {
        var dir = bhPos.LengthSquared() > 1e-4f ? Vector2.Normalize(bhPos) : new Vector2(0f, -1f);
        float[] anglesDeg = [66f, 70f, 62f, 74f, 58f, 78f, 54f, 50f, -66f, -60f, -72f];
        float[] radii = isMelee
            ? [8.0f, 8.6f, 9.2f, 9.8f, 10.5f, 11.5f]
            : [9.5f, 10.2f, 8.8f, 11.0f, 12.0f];

        Vector2? best = null;
        var bestScore = float.MaxValue;
        const float minLaserCenterClearance = 7.8f;
        var preferredRadius = isMelee ? 8.2f : 9.8f;

        foreach (var deg in anglesDeg)
        {
            var rad = deg * (MathF.PI / 180f);
            var rot = RotateVec(dir, rad);
            foreach (var r in radii)
            {
                var cand = rot * r;
                if (cand.Length() > ArenaRadius - 1.5f) continue;
                var clamped = world.Obstacles.ClampOutside(cand, margin: 1.5f);
                if (Vector2.DistanceSquared(clamped, cand) > 0.04f) continue;
                var seg = cand - bhPos;
                var len = seg.Length();
                if (len < 4.0f) continue;
                var laserDistToCenter = MathF.Abs(bhPos.X * seg.Y - bhPos.Y * seg.X) / len;
                if (laserDistToCenter < minLaserCenterClearance) continue;

                var score = (deg < 0f ? 50f : 0f)
                          + MathF.Abs(MathF.Abs(deg) - 67f) * 0.25f
                          + MathF.Abs(r - preferredRadius) * 0.8f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }
        }

        if (best is { } found) return found;
        var fallback = RotateVec(dir, 67f * (MathF.PI / 180f)) * preferredRadius;
        return world.Obstacles.ClampOutside(fallback, margin: 1.5f);
    }

    private static readonly int[] SupportGroup = [0, 1, 2, 3];
    private static readonly int[] DpsGroup = [4, 5, 6, 7];
    private const float StompTowerX = 10f;
    private const float StompTowerPairGap = 1.5f;

    private bool SupportsStackFirst() => !state.StackTargets[0].IsDps();

    private static IAiMove PrepositionForStomp() => AiMove.All(new(0f, 0f));

    private IAiMove StompBlizzardCorners() =>
        AiMove.Create(
                  new(-0.5f, -10f), new(0.5f, -10f), new(-0.5f, -10.5f), new(0.5f, -10.5f),
                  new(-0.5f, 10f), new(0.5f, 10f), new(-0.5f, 10.5f), new(0.5f, 10.5f))
              .NaturalOrder()
              .ApplyPositions(state.KefkaPosition[4].Apply);

    private IAiMove StompStackAndTowers()
    {
        var coords = new Vector2?[8];
        foreach (var r in SupportsStackFirst() ? SupportGroup : DpsGroup) coords[r] = Vector2.Zero;
        PlaceOnTowers(coords, SupportsStackFirst() ? DpsGroup : SupportGroup);
        return AiMove.Create(coords).NaturalOrder().ApplyPositions(state.KefkaPosition[4].Apply);
    }

    private IAiMove StompSwapWest() => StompSwapHalf(0, 2, -StompTowerX);
    private IAiMove StompSwapEast() => StompSwapHalf(1, 3, StompTowerX);

    private IAiMove StompSwapHalf(int pairA, int pairB, float towerX)
    {
        var newTower = SupportsStackFirst() ? SupportGroup : DpsGroup;
        var newStack = SupportsStackFirst() ? DpsGroup : SupportGroup;
        var coords = new Vector2?[8];
        coords[newTower[pairA]] = new Vector2(towerX, -StompTowerPairGap);
        coords[newTower[pairB]] = new Vector2(towerX, StompTowerPairGap);
        coords[newStack[pairA]] = Vector2.Zero;
        coords[newStack[pairB]] = Vector2.Zero;
        return AiMove.Create(coords).NaturalOrder().ApplyPositions(state.KefkaPosition[4].Apply);
    }

    private static void PlaceOnTowers(Vector2?[] coords, int[] group)
    {
        coords[group[0]] = new Vector2(-StompTowerX, -StompTowerPairGap);
        coords[group[2]] = new Vector2(-StompTowerX, StompTowerPairGap);
        coords[group[1]] = new Vector2(StompTowerX, -StompTowerPairGap);
        coords[group[3]] = new Vector2(StompTowerX, StompTowerPairGap);
    }

    private static IAiMove SpreadOutForBigBangAndBlizzard() => BlizzardRingSpots(0f);

    private static IAiMove KeepMovingForBlizzard() => BlizzardRingSpots(0.45f);

    private static IAiMove BlizzardRingSpots(float angleOffset)
    {
        var coords = new Vector2?[8];
        const float radius = 12.5f;
        for (int i = 0; i < 8; i++)
        {
            var angle = angleOffset + i * (MathF.PI / 4f);
            coords[i] = new Vector2(MathF.Sin(angle) * radius, -MathF.Cos(angle) * radius);
        }
        return AiMove.Create(coords).NaturalOrder();
    }
}
