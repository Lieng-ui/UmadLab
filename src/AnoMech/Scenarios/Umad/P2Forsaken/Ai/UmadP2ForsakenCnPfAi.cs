using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

public sealed class UmadP2ForsakenCnPfAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "CN PF 1238 (闲固扇钢)";
    public string? Group => "CN";

    private const float PastMeleeFromCenter = 8.2f;
    private const float FutureMeleeFromCenter = 8.4f;

    private static readonly Vector2?[] CnOddCoords =
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

    private static readonly Vector2?[] CnEvenCoords =
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

    private UmadP2ForsakenState state = null!;
    private SimWorld world = null!;
    private IReadOnlyList<PartyRole> alphaInitial = [];
    private IReadOnlyList<PartyRole> betaInitial = [];
    private List<PartyRole> alpha = [];
    private List<PartyRole> beta = [];
    private readonly Dictionary<PartyRole, float> prevEvenAngles = [];

    public void Run(UmadP2ForsakenState s, SimWorld w)
    {
        state = s;
        world = w;
        prevEvenAngles.Clear();
        var ai = new AiManager(world);
        Init();

        world.Events.Add(32.70f, () => RecordEvenAngles(1));
        world.Events.Add(53.75f, () => RecordEvenAngles(3));
        world.Events.Add(74.80f, () => RecordEvenAngles(5));

        ai.Move(1f, InitialLineup);
        ai.Move(11.30f, TowerPositions(0), jitter: 0f);
        ai.Move(23.60f, TowerPositions(1), jitter: 0f);
        ai.Move(33.60f, AllThingsEndsBait(0, 2), sprint: state.EndAttacks[0] == EndAttack.FuturesEnd);
        ai.Move(38.75f, TowerPositions(2), jitter: 0f, sprint: state.EndAttacks[0] == EndAttack.FuturesEnd);
        ai.Move(44.65f, TowerPositions(3), jitter: 0f);
        ai.Move(54.65f, AllThingsEndsBait(1, 4), sprint: state.EndAttacks[1] == EndAttack.FuturesEnd);
        ai.Move(59.90f, TowerPositions(4), jitter: 0f, sprint: state.EndAttacks[1] == EndAttack.FuturesEnd);
        ai.Move(65.70f, TowerPositions(5), jitter: 0f);
        ai.Move(75.70f, AllThingsEndsBait(2, 6), sprint: state.EndAttacks[2] == EndAttack.FuturesEnd);
        ai.Move(80.90f, TowerPositions(6), jitter: 0f, sprint: state.EndAttacks[2] == EndAttack.FuturesEnd);
        ai.Move(86.75f, TowerPositions(7), jitter: 0f);
        ai.Move(96.75f, GatherAtA());
        ai.Move(101.90f, LastEndBaitAtA(), sprint: state.EndAttacks[3] == EndAttack.FuturesEnd);
    }

    private void RecordEvenAngles(int evenRound)
    {
        prevEvenAngles.Clear();
        var north = state.NewNorthAt(evenRound).RadiansFromNorth;
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

    private void Init()
    {
        var stacks = state.Lockons
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
                if (!used.Contains(role) && state.Lockons[role] == mechanic)
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

        return AiMove.Create(CnOddCoords)
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
            .ApplyPositions(state.NewNorthAt(i).Apply);
    }

    private IAiMove EvenTower(int i)
    {
        var active = SelectActiveRoles(
            i,
            LockonId.ForsakenCone,
            LockonId.ForsakenCone,
            LockonId.ForsakenChariot,
            LockonId.ForsakenChariot);
        return AiMove.Create(CnEvenCoords)
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
            .ApplyPositions(state.NewNorthAt(i).Apply);
    }

    private Func<IAiMove> AllThingsEndsBait(int i, int northIndex)
    {
        var distance = state.EndAttacks[i] == EndAttack.PastsEnd ? -PastMeleeFromCenter : FutureMeleeFromCenter;
        return () => AiMove.All(new(0f, distance))
            .ApplyPositions(state.NewNorthAt(northIndex).Apply);
    }

    private Func<IAiMove> GatherAtA()
        => () => AiMove.All(new(0f, -PastMeleeFromCenter))
            .ApplyPositions(Direction.N.Apply);

    private Func<IAiMove> LastEndBaitAtA()
    {
        var distance = state.EndAttacks[3] == EndAttack.PastsEnd ? -PastMeleeFromCenter : FutureMeleeFromCenter;
        return () => AiMove.All(new(0f, distance))
            .ApplyPositions(Direction.N.Apply);
    }

    private static IAiMove InitialLineup() => AiMove.Create(
        new(-2.5f, -2.5f),
        new(-2.5f, 2.5f),
        new(-7.5f, -2.5f),
        new(-7.5f, 2.5f),
        new(2.5f, -2.5f),
        new(2.5f, 2.5f),
        new(7.5f, -2.5f),
        new(7.5f, 2.5f));
}