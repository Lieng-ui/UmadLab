using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P2Forsaken;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Full;

public sealed class UmadP2FullAi : IScenarioAi<UmadP2FullState>
{
    public string Name => "CN PF Full P2 (1238闲固扇钢/异三角)";

    private const float ForsakenOffset = UmadP2FullScenario.ForsakenOffset;
    private const float PastMeleeFromCenter = 8.2f;
    private const float FutureMeleeFromCenter = 8.4f;

    private static readonly Vector2?[] ForsakenOddCoords =
    [
        new(7.2f, -5.2f),
        new(5.8f, -8.9f),
        new(-8.4f, -3.5f),
        new(-2.6f, -7.2f),
        new(5.8f, -11.1f),
        new(10.0f, -4.5f),
        new(-9.0f, -2.0f),
        new(-10.2f, -3.5f),
    ];

    private static readonly Vector2?[] ForsakenEvenCoords =
    [
        new(9.3f, -6.2f),
        new(8.3f, -8.3f),
        new(-3.7f, -8.3f),
        new(-8.8f, -4.1f),
        new(3.6f, -1.8f),
        new(1.7f, 4.9f),
        new(-4.1f, 3.6f),
        new(-2.6f, -2.4f),
    ];

    private UmadP2ForsakenState fs = null!;
    private SimWorld world = null!;
    private IReadOnlyList<PartyRole> alphaInitial = [];
    private IReadOnlyList<PartyRole> betaInitial = [];
    private List<PartyRole> alpha = [];
    private List<PartyRole> beta = [];
    private readonly Dictionary<PartyRole, float> prevEvenAngles = [];

    public void Run(UmadP2FullState state, SimWorld w)
    {
        fs = state.Forsaken;
        world = w;
        prevEvenAngles.Clear();
        var ai = new AiManager(world);
        var cd = state.CountdownSeconds;
        InitForsakenGroups();

        ai.Move(0.1f, PreForsakenOpenerSpots, jitter: 0.2f);
        ai.Move(cd + 7.4f, UltimateEmbraceSpots, jitter: 0.15f);
        ai.Move(cd + 13.0f, PostEmbraceResetSpots, jitter: 0.2f);

        var tf = cd + ForsakenOffset;
        world.Events.Add(tf + 32.70f, () => RecordEvenAngles(1));
        world.Events.Add(tf + 53.75f, () => RecordEvenAngles(3));
        world.Events.Add(tf + 74.80f, () => RecordEvenAngles(5));

        ai.Move(tf + 1.0f, ForsakenInitialLineup);
        ai.Move(tf + 11.30f, TowerPositions(0), jitter: 0f);
        ai.Move(tf + 23.60f, TowerPositions(1), jitter: 0f);
        ai.Move(tf + 33.60f, AllThingsEndsBait(0, 2), sprint: fs.EndAttacks[0] == EndAttack.FuturesEnd);
        ai.Move(tf + 38.75f, TowerPositions(2), jitter: 0f, sprint: fs.EndAttacks[0] == EndAttack.FuturesEnd);
        ai.Move(tf + 44.65f, TowerPositions(3), jitter: 0f);
        ai.Move(tf + 54.65f, AllThingsEndsBait(1, 4), sprint: fs.EndAttacks[1] == EndAttack.FuturesEnd);
        ai.Move(tf + 59.90f, TowerPositions(4), jitter: 0f, sprint: fs.EndAttacks[1] == EndAttack.FuturesEnd);
        ai.Move(tf + 65.70f, TowerPositions(5), jitter: 0f);
        ai.Move(tf + 75.70f, AllThingsEndsBait(2, 6), sprint: fs.EndAttacks[2] == EndAttack.FuturesEnd);
        ai.Move(tf + 80.90f, TowerPositions(6), jitter: 0f, sprint: fs.EndAttacks[2] == EndAttack.FuturesEnd);
        ai.Move(tf + 86.75f, TowerPositions(7), jitter: 0f);
        ai.Move(tf + 96.75f, GatherAtA());
        ai.Move(tf + 101.90f, LastEndBaitAtA(), sprint: fs.EndAttacks[3] == EndAttack.FuturesEnd);

        ai.Move(cd + 134.5f, PostEmbraceResetSpots, jitter: 0.2f);
        ai.Move(cd + 148.4f, () => FirstWingsSafeSpots(state), jitter: 0.15f);
        ai.Move(cd + 152.4f, TrineCenterWaitSpots, jitter: 0.15f);
        ai.Move(cd + 158.05f, () => TrineAndDoubleWingsSpots(state), jitter: 0.1f, sprint: true);
        ai.Move(cd + 163.0f, UltimateEmbraceSpots, jitter: 0.15f);
        ai.Move(cd + 174.0f, StormRaidwideStackSpots, jitter: 0.2f);
    }

    private void RecordEvenAngles(int evenRound)
    {
        prevEvenAngles.Clear();
        var north = fs.NewNorthAt(evenRound).RadiansFromNorth;
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } member) continue;
            var pos = member.Position;
            var angle = MathF.Atan2(pos.X, -pos.Z) - north;
            while (angle <= -MathF.PI) angle += MathF.Tau;
            while (angle > MathF.PI) angle -= MathF.Tau;
            prevEvenAngles[role] = angle;
        }
    }

    private void InitForsakenGroups()
    {
        var stacks = fs.Lockons
            .Where(pair => pair.Value == LockonId.ForsakenStack)
            .Select(pair => pair.Key)
            .ToList();
        var supportPair = (PartyRole)(((int)stacks[0] + 2) % 4);
        var dpsPair = (PartyRole)((((int)stacks[1] - 2) % 4) + 4);
        var list = new List<PartyRole>([stacks[0], stacks[1], supportPair, dpsPair]);
        list.Sort();
        (list[0], list[1]) = (list[1], list[0]);
        alpha = list;
        alphaInitial = new List<PartyRole>(alpha);
        list = Enum.GetValues<PartyRole>()
            .Where(role => !list.Contains(role))
            .ToList();
        (list[0], list[1]) = (list[1], list[0]);
        beta = list;
        betaInitial = new List<PartyRole>(beta);
    }

    private PartyRole[] SelectActiveRoles(int towerId, params uint[] expectedMechanics)
    {
        var array = towerId is < 3 or 7 ? alpha : beta;
        var used = new HashSet<PartyRole>();
        var result = new PartyRole[expectedMechanics.Length];
        for (var idx = 0; idx < expectedMechanics.Length; idx++)
        {
            var mechanic = expectedMechanics[idx];
            var found = false;
            foreach (var role in array)
            {
                if (!used.Contains(role) && fs.Lockons[role] == mechanic)
                {
                    result[idx] = role;
                    used.Add(role);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                foreach (var role in array)
                {
                    if (!used.Contains(role))
                    {
                        result[idx] = role;
                        used.Add(role);
                        break;
                    }
                }
            }
        }
        return result;
    }

    private PartyRole PassiveRole(int towerId, int order)
    {
        var array = towerId is < 3 or 7 ? betaInitial : alphaInitial;
        return array[order];
    }

    private Func<IAiMove> TowerPositions(int i)
        => () => i % 2 == 1 ? EvenTower(i) : OddTower(i);

    private IAiMove OddTower(int i)
    {
        var active = SelectActiveRoles(
            i,
            LockonId.ForsakenStack,
            LockonId.ForsakenCone,
            LockonId.ForsakenStack,
            LockonId.ForsakenChariot);
        var leftStack = active[0];
        var cone = active[1];
        var rightStack = active[2];
        var chariot = active[3];
        if (i == 0 && (((int)rightStack >= 4) != ((int)cone >= 4)))
            (rightStack, leftStack) = (leftStack, rightStack);
        else if (i > 0
            && prevEvenAngles.TryGetValue(leftStack, out var leftAngle)
            && prevEvenAngles.TryGetValue(rightStack, out var rightAngle)
            && leftAngle > rightAngle)
            (leftStack, rightStack) = (rightStack, leftStack);

        return AiMove.Create(ForsakenOddCoords)
            .Assignments([
                rightStack,
                cone,
                leftStack,
                chariot,
                PassiveRole(i, 0),
                PassiveRole(i, 1),
                PassiveRole(i, 2),
                PassiveRole(i, 3),
            ])
            .ApplyPositions(fs.NewNorthAt(i).Apply);
    }

    private IAiMove EvenTower(int i)
    {
        var active = SelectActiveRoles(
            i,
            LockonId.ForsakenCone,
            LockonId.ForsakenCone,
            LockonId.ForsakenChariot,
            LockonId.ForsakenChariot);
        return AiMove.Create(ForsakenEvenCoords)
            .Assignments([
                active[0],
                active[1],
                active[2],
                active[3],
                PassiveRole(i, 0),
                PassiveRole(i, 1),
                PassiveRole(i, 2),
                PassiveRole(i, 3),
            ])
            .ApplyPositions(fs.NewNorthAt(i).Apply);
    }

    private Func<IAiMove> AllThingsEndsBait(int i, int northIndex)
    {
        var distance = fs.EndAttacks[i] == EndAttack.PastsEnd ? -PastMeleeFromCenter : FutureMeleeFromCenter;
        return () => AiMove.All(new(0f, distance))
            .ApplyPositions(fs.NewNorthAt(northIndex).Apply);
    }

    private Func<IAiMove> GatherAtA()
        => () => AiMove.All(new(0f, -PastMeleeFromCenter))
            .ApplyPositions(Direction.N.Apply);

    private Func<IAiMove> LastEndBaitAtA()
    {
        var distance = fs.EndAttacks[3] == EndAttack.PastsEnd ? -PastMeleeFromCenter : FutureMeleeFromCenter;
        return () => AiMove.All(new(0f, distance))
            .ApplyPositions(Direction.N.Apply);
    }

    private static IAiMove ForsakenInitialLineup() => AiMove.Create(
        new(-2.5f, -2.5f),
        new(-2.5f, 2.5f),
        new(-7.5f, -2.5f),
        new(-7.5f, 2.5f),
        new(2.5f, -2.5f),
        new(2.5f, 2.5f),
        new(7.5f, -2.5f),
        new(7.5f, 2.5f));

    private static IAiMove PreForsakenOpenerSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -6.0f);
        coords[(int)PartyRole.OffTank] = new Vector2(-1.5f, -5.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.5f, 6.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.5f, 6.0f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.8f, 4.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.8f, 4.8f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(3.2f, 6.0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-3.2f, 6.0f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove UltimateEmbraceSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(-0.6f, -6.2f);
        coords[(int)PartyRole.OffTank] = new Vector2(0.6f, -6.2f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.5f, 5.8f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.5f, 5.8f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.6f, 4.6f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.6f, 4.6f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(2.8f, 5.8f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-2.8f, 5.8f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove PostEmbraceResetSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -5.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(-1.8f, 4.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(-1.2f, 5.2f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(1.2f, 5.2f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(-1.5f, 4.2f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(1.5f, 4.2f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(2.4f, 5.2f);
        coords[(int)PartyRole.CasterDps] = new Vector2(-2.4f, 5.2f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove FirstWingsSafeSpots(UmadP2FullState state)
    {
        var sx = state.FirstWingsEastUnsafe ? -2.4f : 2.4f;
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(sx, -3.5f);
        coords[(int)PartyRole.OffTank] = new Vector2(sx, -1.5f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(sx, 2.0f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(sx, 2.6f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(sx, 0.8f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(sx, 1.4f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(sx, 3.0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(sx, 3.4f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove TrineCenterWaitSpots()
    {
        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = new Vector2(0f, -1.2f);
        coords[(int)PartyRole.OffTank] = new Vector2(-1.2f, 0f);
        coords[(int)PartyRole.RegenHealer] = new Vector2(0.8f, 0.8f);
        coords[(int)PartyRole.ShieldHealer] = new Vector2(-0.8f, 0.8f);
        coords[(int)PartyRole.MeleeDpsA] = new Vector2(0.8f, -0.6f);
        coords[(int)PartyRole.MeleeDpsB] = new Vector2(-0.8f, -0.6f);
        coords[(int)PartyRole.PhysRangedDps] = new Vector2(1.2f, 0f);
        coords[(int)PartyRole.CasterDps] = new Vector2(0f, 1.2f);
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove TrineAndDoubleWingsSpots(UmadP2FullState state)
    {
        var (partyIdx, tankIdx) = state.PickTrineSafePair();
        var partyDir = Vector2.Normalize(UmadP2FullState.OuterTrines[partyIdx].Center);
        var tankDir = Vector2.Normalize(UmadP2FullState.OuterTrines[tankIdx].Center);

        var partySpot = partyDir * 14.6f;
        var mtSpot = Rotate(tankDir, -15f * MathF.PI / 180f) * 11.8f;
        var stSpot = Rotate(tankDir, 15f * MathF.PI / 180f) * 19.2f;

        var coords = new Vector2?[8];
        coords[(int)PartyRole.MainTank] = mtSpot;
        coords[(int)PartyRole.OffTank] = stSpot;
        coords[(int)PartyRole.RegenHealer] = partySpot;
        coords[(int)PartyRole.ShieldHealer] = partySpot;
        coords[(int)PartyRole.MeleeDpsA] = partySpot;
        coords[(int)PartyRole.MeleeDpsB] = partySpot;
        coords[(int)PartyRole.PhysRangedDps] = partySpot;
        coords[(int)PartyRole.CasterDps] = partySpot;
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var c = MathF.Cos(radians);
        var s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    private static IAiMove StormRaidwideStackSpots()
    {
        var coords = new Vector2?[8];
        foreach (var role in Enum.GetValues<PartyRole>())
            coords[(int)role] = new Vector2(0f, 1.0f);
        return AiMove.Create(coords).NaturalOrder();
    }
}