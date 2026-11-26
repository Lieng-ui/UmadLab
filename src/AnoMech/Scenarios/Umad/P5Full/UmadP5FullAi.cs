using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullAi : IScenarioAi<UmadP5FullState>
{
    public string Name => "CN PF Full P5 (完整版)";

    private const float FloodMoveSpeed = 8.3f;
    private const float QuadrantRadius = 4.0f;
    private const float ClumpRadius = 1.2f;
    private const float DepartAfterResolve = 0.1f;

    private const float CelestriadMoveSpeed = 7.5f;
    private const float CelestriadPairOffset = 1.0f;
    private const float CelestriadHalfOffset = 2.0f;

    private const float HolyWave1Radius = 8.0f;
    private const float HolyWave2InRadius = 8.0f;
    private const float HolyWave2OutRadius = 12.2f;

    private const float LocusBase = -35f;
    private const float LocusStep = 10f;
    private const float FireRadius = 6f;
    private static readonly float FireClearU = FireRadius * MathF.Sqrt(2f);
    private static readonly float StripInset = FireClearU + 1.5f;
    private const float OuterReach = 19f;
    private const float DistFill = 0.8f;
    private const float DodgeSpeed = 8.3f;
    private const float MeleeComfort = 2.5f;
    private const float SpreadComfort = 6f;
    private const float TargetJitter = 0.6f;
    private const int DeClumpIters = 6;
    private const float DeClumpRate = 0.5f;

    private const float Comfort = 9.75f;
    private const float OuterRing = 13f;
    private const float InnerFallback = 6.5f;
    private const float NoGoRadius = 3.5f;
    private const float ArenaMax = 19f;
    private const float RelaxSpeed = 6.5f;
    private const float DeadZone = 0.25f;
    private const float WRepel = 1.0f;
    private const float WArena = 1.5f;
    private const float WRadialInner = 0.5f;
    private const float WRadialOuter = 0.30f;

    private const float BloomDelay = 14f / 30f;
    private static readonly float[] ExaWaveStart = [3.0f, 5.5f, 8.0f, 10.5f, 13.0f, 15.5f];

    private static readonly Vector2?[] RoleStackSpots =
    [
        new(0.0f, -6.1f),
        new(0.0f, -6.1f),
        new(-5.0f, 6.0f),
        new(-5.0f, 6.0f),
        new(5.4f, 5.4f),
        new(5.4f, 5.4f),
        new(5.4f, 5.4f),
        new(5.4f, 5.4f),
    ];

    private static readonly Dictionary<PartyRole, Vector2> NonTankClockDirs = new()
    {
        [PartyRole.RegenHealer] = Vector2.Normalize(new Vector2(-1f, 0f)),
        [PartyRole.ShieldHealer] = Vector2.Normalize(new Vector2(0f, 1f)),
        [PartyRole.MeleeDpsA] = Vector2.Normalize(new Vector2(-1f, 1f)),
        [PartyRole.MeleeDpsB] = Vector2.Normalize(new Vector2(1f, 1f)),
        [PartyRole.PhysRangedDps] = Vector2.Normalize(new Vector2(-1f, -1f)),
        [PartyRole.CasterDps] = Vector2.Normalize(new Vector2(1f, 0f)),
    };

    private static readonly Vector2?[] OrchestraWave1Spots =
    [
        new(0.0f, -9.2f),
        new(5.8f, -6.5f),
        NonTankClockDirs[PartyRole.RegenHealer] * HolyWave1Radius,
        NonTankClockDirs[PartyRole.ShieldHealer] * HolyWave1Radius,
        NonTankClockDirs[PartyRole.MeleeDpsA] * HolyWave1Radius,
        NonTankClockDirs[PartyRole.MeleeDpsB] * HolyWave1Radius,
        NonTankClockDirs[PartyRole.PhysRangedDps] * HolyWave1Radius,
        NonTankClockDirs[PartyRole.CasterDps] * HolyWave1Radius,
    ];

    private static readonly Vector3[] Quadrants =
    [
        new(0f, 0f, -QuadrantRadius),
        new(QuadrantRadius, 0f, 0f),
        new(0f, 0f, QuadrantRadius),
        new(-QuadrantRadius, 0f, 0f),
    ];

    private static readonly Vector2[] ForsakenSafeSpots =
    [
        new(-7.0f, 7.0f),
        new(-7.0f, -7.0f),
        new(7.0f, -7.2f),
        new(7.0f, 7.0f),
        new(-7.0f, 7.0f),
    ];

    private UmadP5ExaflaresState exaState = null!;
    private SimWorld exaWorld = null!;
    private readonly bool[] relaxMoving = new bool[8];
    private bool spreadPhase;
    private float innerRing = InnerFallback;

    public void Run(UmadP5FullState state, SimWorld world)
    {
        exaState = state.Exaflares;
        exaWorld = world;
        spreadPhase = false;
        Array.Clear(relaxMoving);

        var ai = new AiManager(world);
        var t0 = state.CountdownSeconds + state.PreTargetableSec;
        var tFlood = t0 + UmadP5FullScenario.FloodBaseSec;
        var tOrch1 = t0 + UmadP5FullScenario.Orchestra1BaseSec;
        var tCel = t0 + UmadP5FullScenario.CelestriadBaseSec;
        var tExa = t0 + UmadP5FullScenario.ExaflaresBaseSec;
        var tOrch2 = t0 + UmadP5FullScenario.Orchestra2BaseSec;

        ai.Move(0.1f, () => AiMove.All(new Vector2(0f, 4.5f)), jitter: 0.6f);
        ai.Move(t0 + 0.5f, () => AiMove.Create(RoleStackSpots).NaturalOrder(), jitter: 0.45f, sprint: true);

        ai.Move(tFlood + UmadP5FloodScenario.FirstTelegraphAt, () => ClumpAt(QuadrantAt(state.Flood, 0)), jitter: 0f, sprint: true);

        for (var tick = 1; tick < UmadP5FloodScenario.TickCount; tick++)
        {
            var quadrant = QuadrantAt(state.Flood, tick);
            if (quadrant == QuadrantAt(state.Flood, tick - 1)) continue;

            var previousResolve = tFlood
                + UmadP5FloodScenario.FirstTelegraphAt
                + (tick - 1) * UmadP5FloodScenario.TelegraphStagger
                + UmadP5FloodScenario.ResolveDelayAfterTelegraph;
            world.Events.Add(previousResolve + DepartAfterResolve, () => MoveToQuadrant(world, quadrant));
        }

        ScheduleOrchestraAi(
            ai, world, tOrch1,
            state.Orchestra1Wave1Holy,
            state.Orchestra1FlareTank,
            state.Orchestra1HolyTank,
            preMoveAt: t0 + 30.2f);

        world.Events.Add(tCel + 7.60f, () => PlaceCelestriadSet(world, state.Celestriad, 0, half: 0f));
        world.Events.Add(tCel + 11.20f, () => PlaceCelestriadSet(world, state.Celestriad, 0, HalfFor(state.Celestriad, 0)));
        world.Events.Add(tCel + 15.50f, () => PlaceCelestriadSet(world, state.Celestriad, 1, half: 0f));
        world.Events.Add(tCel + 21.50f, () => PlaceCelestriadSet(world, state.Celestriad, 2, half: 0f));
        world.Events.Add(tCel + 23.30f, () => PlaceCelestriadSet(world, state.Celestriad, 2, HalfFor(state.Celestriad, 2)));
        ai.Move(tCel + 28.50f, () => AiMove.Create(RoleStackSpots).NaturalOrder(), jitter: 0.45f, sprint: true);

        for (var n = 0; n < 6; n++)
        {
            var waveIndex = n;
            var safeMove = waveIndex == 0 ? 2.0f : ExaWaveStart[waveIndex] + 1.7f + BloomDelay;
            world.Events.Add(tExa + safeMove, () => PlaceExaflareWave(waveIndex));
        }

        world.Events.Add(tExa + 22.75f, () =>
        {
            spreadPhase = true;
            innerRing = ResolveInnerRing();
            Array.Clear(relaxMoving);
        });
        world.Events.Add(tExa + 25.09f, () => spreadPhase = false);
        state.Exaflares.SpreadTick = RelaxStep;

        ScheduleOrchestraAi(
            ai, world, tOrch2,
            state.Orchestra2Wave1Holy,
            state.Orchestra2FlareTank,
            state.Orchestra2HolyTank,
            preMoveAt: tExa + 25.60f);

        ai.Move(t0 + 150.0f, () => AiMove.All(ForsakenSafeSpots[0]), jitter: 0.40f, sprint: true);
        ai.Move(t0 + 160.15f, () => AiMove.All(ForsakenSafeSpots[1]), jitter: 0.40f, sprint: true);
        ai.Move(t0 + 168.35f, () => AiMove.All(ForsakenSafeSpots[2]), jitter: 0.40f, sprint: true);
        ai.Move(t0 + 176.50f, () => AiMove.All(ForsakenSafeSpots[3]), jitter: 0.40f, sprint: true);
        ai.Move(t0 + 184.65f, () => AiMove.All(ForsakenSafeSpots[4]), jitter: 0.40f, sprint: true);
    }

    private static void ScheduleOrchestraAi(
        AiManager ai,
        SimWorld world,
        float tOrch,
        IReadOnlyList<PartyRole> wave1Holy,
        PartyRole flareTank,
        PartyRole holyTank,
        float preMoveAt)
    {
        ai.Move(preMoveAt, () => AiMove.Create(OrchestraWave1Spots).NaturalOrder(), jitter: 0.25f, sprint: true);
        ai.Move(tOrch + 6.35f, () => BuildOrchestraWave2Move(wave1Holy), jitter: 0.20f, sprint: true);
        world.Events.Add(tOrch + 9.50f, () => MoveOrchestraWave3(world, flareTank, holyTank));
        ai.Move(tOrch + 13.10f, () => AiMove.Create(RoleStackSpots).NaturalOrder(), jitter: 0.45f, sprint: true);
    }

    private static IAiMove BuildOrchestraWave2Move(IReadOnlyList<PartyRole> wave1Holy)
    {
        var wave1Set = new HashSet<PartyRole>(wave1Holy);
        var spots = new Vector2?[8];
        spots[0] = new Vector2(0.0f, -9.2f);
        spots[1] = new Vector2(0.2f, -9.0f);
        for (var i = 2; i < 8; i++)
        {
            var role = (PartyRole)i;
            var dir = NonTankClockDirs[role];
            var radius = wave1Set.Contains(role) ? HolyWave2OutRadius : HolyWave2InRadius;
            spots[i] = dir * radius;
        }
        return AiMove.Create(spots).NaturalOrder();
    }

    private static void MoveOrchestraWave3(SimWorld world, PartyRole flareTankRole, PartyRole holyTankRole)
    {
        if (world.Party.Get(flareTankRole) is { } flareTank && flareTank.IsAlive())
            flareTank.MoveTo(new Vector3(0.0f, 0f, -18.5f), FloodMoveSpeed);
        if (world.Party.Get(holyTankRole) is { } holyTank && holyTank.IsAlive())
            holyTank.MoveTo(new Vector3(0.0f, 0f, -8.6f), FloodMoveSpeed);

        Vector3[] southSpots =
        [
            new(-2.2f, 0f, 13.0f),
            new(0.0f, 0f, 14.5f),
            new(-1.2f, 0f, 12.2f),
            new(1.2f, 0f, 12.2f),
            new(-1.8f, 0f, 14.8f),
            new(2.0f, 0f, 13.5f),
        ];

        for (var i = 2; i < 8; i++)
        {
            var bot = world.Party.Get((PartyRole)i);
            if (bot is null || !bot.IsAlive()) continue;
            bot.MoveTo(southSpots[i - 2], FloodMoveSpeed);
        }
    }

    private void PlaceExaflareWave(int n)
    {
        var leftWave = (n % 2) == 0;
        var lanes = UsableBands(n);
        if (lanes.Count == 0) return;

        var picks = new List<ExaPick>(8);
        for (var slot = 0; slot < 8; slot++)
        {
            var bot = exaWorld.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;

            var inner = IsInnerRole((PartyRole)slot);
            var (crit, perp) = ToCritPerp(bot.Position, leftWave);
            var want = (inner ? 0f : crit) + ExaJitter();
            picks.Add(new ExaPick(bot, PlaceInLanes(lanes, want), perp, inner));
        }

        DeClump(picks, lanes);

        foreach (var pk in picks)
        {
            var dest = FromCritPerp(pk.Crit, pk.Perp, leftWave);
            pk.Bot.MoveTo(dest, DodgeSpeed);
        }
    }

    private void RelaxStep(float dt)
    {
        if (!spreadPhase) return;

        var agents = new List<(SimCharacter c, Vector2 p)>(8);
        for (var i = 0; i < 8; i++)
            if (exaWorld.Party.Get(i) is { } m && m.IsAlive())
                agents.Add((m, new Vector2(m.Position.X, m.Position.Z)));

        for (var slot = 0; slot < 8; slot++)
        {
            var bot = exaWorld.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;
            var p = new Vector2(bot.Position.X, bot.Position.Z);

            var desired = Vector2.Zero;

            foreach (var (c, q) in agents)
            {
                if (ReferenceEquals(c, bot)) continue;
                var d = p - q;
                var dist = d.Length();
                if (dist >= Comfort) continue;
                var dir = dist > 1e-3f ? d / dist : Spoke(slot);
                desired += dir * (Comfort - dist) * WRepel;
            }

            var inner = IsInnerRole((PartyRole)slot);
            var r = p.Length();
            var rdir = r > 1e-3f ? p / r : Spoke(slot);
            if (inner)
            {
                if (r > innerRing) desired += rdir * (innerRing - r) * WRadialInner;
                else if (r < NoGoRadius) desired += rdir * (NoGoRadius - r) * WRadialInner;
            }
            else
            {
                desired += rdir * (OuterRing - r) * WRadialOuter;
            }
            if (r > ArenaMax) desired += rdir * (ArenaMax - r) * WArena;

            if (desired.Length() > DeadZone)
            {
                var step = desired.Length() > 5f ? desired * (5f / desired.Length()) : desired;
                var t = p + step;
                if (t.Length() > ArenaMax) t *= ArenaMax / t.Length();
                bot.MoveTo(new Vector3(t.X, 0f, t.Y), RelaxSpeed);
                relaxMoving[slot] = true;
            }
            else if (relaxMoving[slot])
            {
                bot.MoveTo(new Vector3(p.X, 0f, p.Y), RelaxSpeed);
                relaxMoving[slot] = false;
            }
        }
    }

    private float ResolveInnerRing()
    {
        var boss = exaWorld.Children.OfType<SimEnemy>()
            .FirstOrDefault(e => e.IsAlive() && e.BNpcBaseId == BNpcBaseId.KefkaP5);
        var hitbox = boss?.HitboxRadius ?? 3.5f;
        return MathF.Max(hitbox + 3f, 6f);
    }

    private static bool IsInnerRole(PartyRole r) =>
        r.IsTank() || r == PartyRole.MeleeDpsA || r == PartyRole.MeleeDpsB;

    private List<(float lo, float hi)> UsableBands(int n)
    {
        var bands = new List<(float lo, float hi)> { Fill(Band(n, 0)) };
        if (OuterAvailable(n)) bands.Add(Fill(Band(n, 1)));
        return bands;
    }

    private static float PlaceInLanes(List<(float lo, float hi)> lanes, float want)
    {
        var best = want;
        var bestD = float.MaxValue;
        foreach (var (lo, hi) in lanes)
        {
            var c = Math.Clamp(want, lo, hi);
            var d = MathF.Abs(c - want);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    private void DeClump(List<ExaPick> picks, List<(float lo, float hi)> lanes)
    {
        for (var iter = 0; iter < DeClumpIters; iter++)
            for (var i = 0; i < picks.Count; i++)
            {
                var pi = picks[i];
                var push = 0f;
                for (var j = 0; j < picks.Count; j++)
                {
                    if (j == i) continue;
                    var pj = picks[j];
                    var comfort = pi.Inner && pj.Inner ? MeleeComfort : SpreadComfort;
                    var dc = pi.Crit - pj.Crit;
                    var dp = pi.Perp - pj.Perp;
                    var dist = MathF.Sqrt(dc * dc + dp * dp);
                    if (dist >= comfort) continue;
                    var dir = dist > 1e-3f ? dc / dist : MathF.Sign(i - j);
                    push += dir * (comfort - dist);
                }
                if (MathF.Abs(push) < 1e-3f) continue;
                pi.Crit = PlaceInLanes(lanes, pi.Crit + push * DeClumpRate);
            }
    }

    private float ExaJitter() => ((float)exaWorld.Rng.NextDouble() - 0.5f) * TargetJitter;

    private static (float crit, float perp) ToCritPerp(Vector3 pos, bool leftWave)
    {
        var a = pos.X - pos.Z;
        var b = pos.X + pos.Z;
        return leftWave ? (a, b) : (b, a);
    }

    private static Vector3 FromCritPerp(float crit, float perp, bool leftWave)
    {
        var a = leftWave ? crit : perp;
        var b = leftWave ? perp : crit;
        return new Vector3((a + b) * 0.5f, 0f, (b - a) * 0.5f);
    }

    private bool OuterAvailable(int n)
    {
        var (lo, hi) = PairLoHi(n);
        return MathF.Abs((lo + hi) * 0.5f) > 1f;
    }

    private (float lo, float hi) PairLoHi(int n)
    {
        var order = (n % 2) == 0 ? exaState.LeftOrder : exaState.RightOrder;
        var pairBase = (n / 2) * 2;
        var l1 = Locus(order[pairBase]);
        var l2 = Locus(order[pairBase + 1]);
        return (MathF.Min(l1, l2), MathF.Max(l1, l2));
    }

    private (float lo, float hi) Band(int n, int idx)
    {
        var (lo, hi) = PairLoHi(n);
        if (idx == 0) return (lo + StripInset, hi - StripInset);
        return (lo + hi) * 0.5f < 0f
            ? (hi + StripInset, OuterReach)
            : (-OuterReach, lo - StripInset);
    }

    private static float Locus(int lineIdx) => LocusBase + LocusStep * lineIdx;

    private static (float lo, float hi) Fill((float lo, float hi) s)
    {
        var mid = (s.lo + s.hi) * 0.5f;
        var half = (s.hi - s.lo) * 0.5f * DistFill;
        return (mid - half, mid + half);
    }

    private static Vector2 Spoke(int slot) => new(MathF.Cos(slot * 0.785f), MathF.Sin(slot * 0.785f));

    private sealed class ExaPick(SimCharacter bot, float crit, float perp, bool inner)
    {
        public readonly SimCharacter Bot = bot;
        public float Crit = crit;
        public readonly float Perp = perp;
        public readonly bool Inner = inner;
    }

    private static int QuadrantAt(UmadP5FloodState flood, int tick)
    {
        var rotations = Math.Max(0, tick - 1);
        var step = flood.RotationClockwise ? rotations : -rotations;
        return ((flood.StartQuadrant + step) % 4 + 4) % 4;
    }

    private static void MoveToQuadrant(SimWorld world, int quadrant)
    {
        for (var slot = 0; slot < 8; slot++)
        {
            var bot = world.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;
            bot.MoveTo(ClumpSpot(quadrant, slot), FloodMoveSpeed);
        }
    }

    private static IAiMove ClumpAt(int quadrant)
        => AiMove.Create(Enumerable.Range(0, 8)
            .Select(slot => ClumpSpot(quadrant, slot))
            .Select(spot => (Vector2?)new Vector2(spot.X, spot.Z))
            .ToArray()).NaturalOrder();

    private static Vector3 ClumpSpot(int quadrant, int slot)
    {
        var angle = slot * MathF.PI / 4f;
        return Quadrants[quadrant] + new Vector3(MathF.Sin(angle) * ClumpRadius, 0f, MathF.Cos(angle) * ClumpRadius);
    }

    private static float HalfFor(UmadP5CelestriadState celestriad, int set) =>
        celestriad.AeroVariant[set] is { } choice
            ? (choice == CatastrophicChoice.Aero ? 1f : -1f)
            : 0f;

    private static void PlaceCelestriadSet(SimWorld world, UmadP5CelestriadState celestriad, int set, float half)
    {
        var party = world.Party;
        var freeRoles = celestriad.PlayerDebuffElement.Where(kv => kv.Value is null).Select(kv => kv.Key).ToArray();

        foreach (var group in celestriad.SetActiveTowers[set]
                     .Select(towerIndex => celestriad.AllTowers[towerIndex])
                     .GroupBy(tower => tower.Element))
        {
            var debuffedRoles = celestriad.PlayerDebuffElement
                .Where(kv => kv.Value is not null && celestriad.ElementForSet(kv.Key, set) == group.Key)
                .Select(kv => kv.Key)
                .ToArray();

            var towers = group.ToArray();
            for (var i = 0; i < towers.Length; i++)
                PlaceAtTower(party, towers[i], i == 0 ? debuffedRoles : freeRoles, half);
        }
    }

    private static void PlaceAtTower(SimParty party, CelestriadTower tower, IReadOnlyList<PartyRole> roles, float half)
    {
        var inward = Vector3.Normalize(-tower.Position);
        var lateral = new Vector3(-inward.Z, 0f, inward.X);

        for (var i = 0; i < roles.Count; i++)
        {
            var bot = party.Get(roles[i]);
            if (bot is null || !bot.IsAlive()) continue;

            var side = i == 0 ? -1f : 1f;
            var dest = tower.Position + lateral * (side * CelestriadPairOffset) + inward * (half * CelestriadHalfOffset);
            bot.MoveTo(dest, CelestriadMoveSpeed);
        }
    }
}
