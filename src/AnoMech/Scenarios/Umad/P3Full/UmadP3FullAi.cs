using System;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3LimitCut;
using static AnoMech.Scenarios.Umad.UmadConstants;
using static AnoMech.Scenarios.Umad.P3Full.UmadP3FullState;

namespace AnoMech.Scenarios.Umad.P3Full;

public sealed class UmadP3FullAi : IScenarioAi<UmadP3FullState>
{
    public string Name => "CN PF Full P3 (一运+二运黑洞完整)";

    private const float T0 = UmadP3FullScenario.PreTargetableDurationSec;
    private const float LcBase = UmadP3FullScenario.LimitCutBaseSec;
    private const float ThunderClearDistance = 10f;
    private const float ThunderClearanceInterval = 0.4f;

    private readonly UmadP3FullBlackHoleAi blackHoleAi = new();
    private UmadP3FullState state = null!;
    private SimWorld world = null!;
    private (Vector2 Center, float Rotation)? implosionChaos;

    private float Hold => SpotHeading(state.FireSpot);

    public void Run(UmadP3FullState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        implosionChaos = null;
        var ai = new AiManager(world);
        var cd = state.CountdownSeconds;

        ai.Move(0.1f, OpenerPrepSpots, jitter: 0.2f);
        ai.Move(cd + T0 - 0.8f, DecisiveBattleSpots, jitter: 0.15f);
        ai.Move(cd + T0 + 4.2f, BowelsCenterSpots, jitter: 0.2f);
        ai.Move(cd + T0 + 26.2f, PullExdeathAndPreShortSpots, jitter: 0.15f, sprint: true);
        ai.Move(cd + T0 + 34.8f, ShortDebuffSpots, jitter: 0.12f, sprint: true);
        ai.Move(cd + T0 + 43.2f, ThunderTbSpots, jitter: 0.15f, sprint: true);
        ai.Move(cd + T0 + 54.6f, PreImplosionSpots, jitter: 0.15f, sprint: true);
        ai.Move(cd + T0 + 59.5f, () => DodgeImplosion(0), jitter: 0.1f);
        ai.Move(cd + T0 + 64.6f, () => DodgeImplosion(1), jitter: 0.1f);
        ai.Move(cd + T0 + 66.52f, LongDebuffSpots, jitter: 0.08f, sprint: true);

        ai.Move(cd + T0 + 70.2f, PreLimitCutStackSpots, jitter: 0.12f, sprint: true);
        ai.Move(cd + T0 + LcBase + 4.2f, UmbraBaitOut, arrivalTime: cd + T0 + LcBase + 7.4f, sprint: true);
        ai.Move(cd + T0 + LcBase + 8.3f, TanksIntoVacuumStack, arrivalTime: cd + T0 + LcBase + 11.8f);
        ai.Move(cd + T0 + LcBase + 8.4f, UmbraBaitBack, arrivalTime: cd + T0 + LcBase + 12.6f, sprint: true);
        world.Events.Add(cd + T0 + LcBase + 14.94f, MainTankLimitBreakIfBothTanksAreBots);
        world.Events.Add(cd + T0 + LcBase + 15.30f, FaceForVacuumWave);
        ai.Move(cd + T0 + LcBase + 17.5f, StackForCyclones, arrivalTime: cd + T0 + LcBase + 19.6f);
        ai.Move(cd + T0 + LcBase + 20.15f, LimitCutChargeSpots, jitter: 0.08f, sprint: true);

        ai.Move(cd + T0 + LcBase + 32.7f, PostLcThunderClear, arrivalTime: cd + T0 + LcBase + 37.5f);
        world.Events.Add(cd + T0 + LcBase + 33.6f, MainTankOntoExdeath);
        ai.Move(cd + T0 + LcBase + 38.9f, SwapPostLcThunderTanks, arrivalTime: cd + T0 + LcBase + 41.2f);
        SchedulePostLcThunderClearance(cd + T0 + LcBase + 36.5f, cd + T0 + LcBase + 38.9f, cd + T0 + LcBase + 42.2f);
        ai.Move(cd + T0 + LcBase + 43.0f, PostLcDecisiveSpots, jitter: 0.15f);
        ai.Move(cd + T0 + LcBase + 50.0f, PostLcThunderClear, arrivalTime: cd + T0 + LcBase + 54.8f);
        world.Events.Add(cd + T0 + LcBase + 50.9f, MainTankOntoExdeath);
        ai.Move(cd + T0 + LcBase + 56.15f, SwapPostLcThunderTanks, arrivalTime: cd + T0 + LcBase + 58.4f);
        SchedulePostLcThunderClearance(cd + T0 + LcBase + 53.8f, cd + T0 + LcBase + 56.15f, cd + T0 + LcBase + 59.5f);
        blackHoleAi.RunAt(state.BlackHole, world, cd + T0 + UmadP3FullScenario.BlackHoleBaseSec);
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static IAiMove OpenerPrepSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = new Vector2(-12.0f, 0.0f);
        coords[(int)PartyRole.MainTank] = new Vector2(12.0f, 0.0f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(4.0f, 1.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(-5.5f, 2.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-7.5f, 1.0f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(-8.0f, -3.5f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(6.5f, 1.0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(11.5f, 0.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove DecisiveBattleSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = new Vector2(-14.0f, 0.0f);
        coords[(int)PartyRole.MainTank] = new Vector2(5.0f, 0.0f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(4.0f, 0.5f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(-5.5f, 2.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-8.0f, 0.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(-8.2f, -4.5f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(6.5f, 0.8f);
        coords[(int)PartyRole.CasterDps] = new Vector2(11.5f, 0.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove BowelsCenterSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = new Vector2(4.8f, -0.4f);
        coords[(int)PartyRole.MainTank] = new Vector2(-5.0f, 0.4f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(2.0f, 2.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(-2.0f, 2.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-2.5f, -2.5f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(2.5f, -2.5f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(0.0f, 3.5f);
        coords[(int)PartyRole.CasterDps] = new Vector2(0.0f, -3.5f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove PullExdeathAndPreShortSpots()
    {
        var coords = BuildCheeseDebuffCoords();
        coords[(int)PartyRole.OffTank] = OnCircle2(SpotHeading(state.FireSpot), 15.8f);
        coords[(int)PartyRole.MainTank] = OnCircle2(SpotHeading(state.WaterSpot), 17.5f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove ShortDebuffSpots()
        => AiMove.Create(BuildCheeseDebuffCoords()).NaturalOrder();

    private Vector2?[] BuildCheeseDebuffCoords()
    {
        var coords = new Vector2?[8];
        var fHead = SpotHeading(state.FireSpot);
        var stackSpot = OnCircle2(fHead, 15.8f);
        var spreadOffset = 55f * MathF.PI / 180f;
        var supSpot = OnCircle2(fHead + spreadOffset, 9.2f);
        var dpsSpot = OnCircle2(fHead - spreadOffset, 9.2f);
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (role == state.FireSupport) coords[(int)role] = supSpot;
            else if (role == state.FireDps) coords[(int)role] = dpsSpot;
            else coords[(int)role] = stackSpot;
        }
        return coords;
    }

    private IAiMove ThunderTbSpots()
    {
        var coords = new Vector2?[8];
        var exdeathPos = state.Objects.Exdeath is { } ex
            ? new Vector2(ex.Position.X, ex.Position.Z)
            : OnCircle2(SpotHeading(state.WaterSpot), 12.8f);
        var tbSpot = exdeathPos.LengthSquared() > 1f
            ? exdeathPos - Vector2.Normalize(exdeathPos) * 1.4f
            : OnCircle2(SpotHeading(state.WaterSpot), 11.4f);
        var safeSpot = OnCircle2(SpotHeading(state.FireSpot), 15.2f);
        foreach (var role in Enum.GetValues<PartyRole>())
            coords[(int)role] = role == PartyRole.MainTank ? tbSpot : safeSpot;
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove PreImplosionSpots()
    {
        var coords = new Vector2?[8];
        var fHead = SpotHeading(state.FireSpot);
        var anchor = OnCircle2(fHead, 10.5f);
        var mtSpot = OnCircle2(fHead + 15f * MathF.PI / 180f, 15.5f);
        foreach (var role in Enum.GetValues<PartyRole>())
            coords[(int)role] = role == PartyRole.OffTank ? mtSpot : anchor;
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove DodgeImplosion(int hitIndex)
    {
        var boss = state.Objects.Chaos;
        if (boss is null) return PreImplosionSpots();
        if (hitIndex == 0 || implosionChaos is null)
            implosionChaos = (new Vector2(boss.Position.X, boss.Position.Z), boss.Rotation);

        var (c, rot) = implosionChaos.Value;
        var baseAxis = rot + (state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f);
        var safeAngle = hitIndex == 0 ? baseAxis + MathF.PI / 2f : baseAxis;
        var spot = PickInBoundsSafeSpot(c, safeAngle);

        var coords = new Vector2?[8];
        foreach (var role in Enum.GetValues<PartyRole>())
            coords[(int)role] = spot;
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 PickInBoundsSafeSpot(Vector2 center, float angle)
    {
        var dirA = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
        var dirB = -dirA;
        var candA = center + dirA * 4.0f;
        var candB = center + dirB * 4.0f;
        var chosen = candA.LengthSquared() <= candB.LengthSquared() ? candA : candB;
        if (chosen.Length() > 17.5f) chosen = Vector2.Normalize(chosen) * 17.5f;
        return chosen;
    }

    private IAiMove LongDebuffSpots()
    {
        var coords = new Vector2?[8];
        var stackSpot = OnCircle2(SpotHeading(state.FireSpot), 15.8f);
        var fHead = SpotHeading(state.FireSpot);
        var spreadOffset = 55f * MathF.PI / 180f;
        var supSpot = OnCircle2(fHead + spreadOffset, 9.2f);
        var dpsSpot = OnCircle2(fHead - spreadOffset, 9.2f);
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (role == state.FireSupport) coords[(int)role] = supSpot;
            else if (role == state.FireDps) coords[(int)role] = dpsSpot;
            else coords[(int)role] = stackSpot;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private Vector2 VacuumStackSpot(int slot)
    {
        var spread = OnCircle2(Hold + MathF.PI / 2f, (slot % 4 - 1.5f) * 1.0f) + OnCircle2(Hold, slot / 4 * 1.0f);
        return OnCircle2(Hold, 7.2f) + spread;
    }

    private IAiMove PreLimitCutStackSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = OnCircle2(Hold, 14.5f) - OnCircle2(Hold + MathF.PI / 2f, 1.5f);
        coords[(int)PartyRole.MainTank] = OnCircle2(Hold, 17.2f) + OnCircle2(Hold + MathF.PI / 2f, 1.8f);
        for (var slot = 2; slot < 8; slot++)
            coords[slot] = VacuumStackSpot(slot);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove UmbraBaitOut()
        => AiMove.Single(state.BaitRole, OnCircle2(Hold + MathF.PI, 17.7f));

    private IAiMove TanksIntoVacuumStack()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = VacuumStackSpot((int)PartyRole.OffTank);
        coords[(int)PartyRole.MainTank] = VacuumStackSpot((int)PartyRole.MainTank);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove UmbraBaitBack()
        => AiMove.Single(state.BaitRole, VacuumStackSpot((int)state.BaitRole));

    private void MainTankLimitBreakIfBothTanksAreBots()
    {
        if (world.Party.Get(PartyRole.MainTank) is not { } mainTank) return;
        if (!world.Party.IsBotDriven(mainTank)) return;
        world.Party.UseLimitBreak(PartyRole.MainTank);
    }

    private void FaceForVacuumWave()
    {
        if (state.Objects.Exdeath is not { } exdeath) return;
        for (var slot = 0; slot < 8; slot++)
        {
            var member = world.Party.Get(slot);
            if (member is null || !member.IsAlive() || member is not ISimPartyMember pm) continue;
            if (member is SimPlayer && !DebugBotControl.Enabled) continue;
            var away = member.Position - exdeath.Position;
            member.Face(state.Winds[pm.Role] == Wind.Headwind ? member.Position + away : exdeath.Position);
        }
    }

    private IAiMove StackForCyclones()
    {
        var away = state.Objects.Exdeath is { } exdeath && exdeath.Position.LengthSquared() > 1f
            ? MathF.Atan2(-exdeath.Position.X, -exdeath.Position.Z)
            : Hold + MathF.PI;
        var centre = OnCircle2(away, 4.8f);
        var coords = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
            coords[slot] = centre + OnCircle2(away + MathF.PI / 2f, (slot % 3 - 1f) * 0.9f) + OnCircle2(away, (slot / 3) * 0.9f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove LimitCutChargeSpots()
    {
        var coords = new Vector2?[8];
        for (var k = 0; k < 8; k++)
            coords[(int)state.Numbers[k]] = OnCircle2(state.SafeHeading(k), 19.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove PostLcThunderClear()
    {
        var coords = new Vector2?[8];
        var exPos = state.Objects.Exdeath is { } ex
            ? Flat(ex.Position)
            : (world.Party.Get(PartyRole.MainTank) is { } mt ? Flat(mt.Position) : OnCircle2(Hold, 8.0f));
        var toCenter = exPos.LengthSquared() > 1f ? Vector2.Normalize(-exPos) : new Vector2(0f, 1f);
        var stStandby = exPos + toCenter * 10.5f;
        if (stStandby.Length() > 17.5f) stStandby = Vector2.Normalize(stStandby) * 17.5f;
        coords[(int)PartyRole.OffTank] = stStandby;
        for (var slot = 2; slot < 8; slot++)
        {
            var baseSpot = exPos + toCenter * 11.5f;
            if (baseSpot.Length() > 16.5f) baseSpot = Vector2.Normalize(baseSpot) * 16.5f;
            var perp = new Vector2(-toCenter.Y, toCenter.X);
            coords[slot] = baseSpot + perp * ((slot % 3 - 1) * 1.5f) + toCenter * ((slot / 3) * 1.4f);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private void MainTankOntoExdeath()
    {
        if (state.Objects.Exdeath is not { } exdeath) return;
        if (world.Party.Get(PartyRole.MainTank) is { } st && st.IsAlive()) st.Follow(exdeath);
    }

    private IAiMove SwapPostLcThunderTanks()
    {
        var a = world.Party.Get(PartyRole.MainTank);
        var b = world.Party.Get(PartyRole.OffTank);
        if (a is null || b is null) return AiMove.Create().NaturalOrder();
        var coords = new Vector2?[8];
        coords[(int)PartyRole.OffTank] = Flat(a.Position);
        coords[(int)PartyRole.MainTank] = Flat(b.Position);
        return AiMove.Create(coords).NaturalOrder();
    }

    private void SchedulePostLcThunderClearance(float fromTime, float swapTime, float toTime)
    {
        for (var t = fromTime; t < swapTime; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(PartyRole.MainTank, PartyRole.OffTank));
        for (var t = swapTime; t <= toTime; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(PartyRole.OffTank, null));
    }

    private void EnforceThunderClearanceOnce(PartyRole first, PartyRole? second)
    {
        if (second is { } secondRole
            && world.Party.Get(first) is { } firstMember && firstMember.IsAlive()
            && world.Party.Get(secondRole) is { } secondMember && secondMember.IsAlive())
        {
            var firstPos = Flat(firstMember.Position);
            var secondPos = Flat(secondMember.Position);
            if ((secondPos - firstPos).LengthSquared() < ThunderClearDistance * ThunderClearDistance)
            {
                var away = firstPos.LengthSquared() > 1e-4f ? Vector2.Normalize(-firstPos) : new Vector2(0f, -1f);
                var seat = firstPos + RotateVec(away, MathF.PI / 2f) * ThunderClearDistance;
                if (seat.LengthSquared() > 361f)
                    seat = Vector2.Normalize(seat) * 19f;
                secondMember.MoveTo(new Vector3(seat.X, 0f, seat.Y));
            }
        }

        if (state.Objects.Exdeath is not { } exdeath || !exdeath.IsAlive()) return;
        var bossPos = Flat(exdeath.Position);
        for (var i = 0; i < 8; i++)
        {
            var role = (PartyRole)i;
            if (role == first || (second is { } sec && role == sec)) continue;
            if (world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
            var offset = Flat(member.Position) - bossPos;
            if (offset.LengthSquared() >= ThunderClearDistance * ThunderClearDistance) continue;
            var dir = offset.LengthSquared() > 1e-4f ? Vector2.Normalize(offset) : new Vector2(1f, 0f);
            var target = bossPos + dir * ThunderClearDistance;
            if (target.LengthSquared() > 361f)
                target = Vector2.Normalize(target) * 19f;
            member.MoveTo(new Vector3(target.X, 0f, target.Y));
        }
    }

    private static Vector2 RotateVec(Vector2 v, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private IAiMove PostLcDecisiveSpots()
    {
        var chaosPos = state.Objects.Chaos is { } ch ? Flat(ch.Position) : OnCircle2(Hold, 6.0f);
        var exdeathPos = state.Objects.Exdeath is { } ex ? Flat(ex.Position) : OnCircle2(Hold + MathF.PI / 2f, 6.0f);
        var coords = new Vector2?[8];
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var isChaosSide = role is PartyRole.MainTank or PartyRole.ShieldHealer or PartyRole.MeleeDpsA or PartyRole.MeleeDpsB;
            coords[(int)role] = isChaosSide ? chaosPos : exdeathPos;
        }
        return AiMove.Create(coords).NaturalOrder();
    }
}
