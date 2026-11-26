using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P2Forsaken;

namespace AnoMech.Scenarios.Umad.P2Full;

public sealed class UmadP2FullState
{
    public float CountdownSeconds { get; } = 5.0f;
    public UmadP2ForsakenState Forsaken { get; }
    public bool FirstWingsEastUnsafe { get; }
    public int[] TrineWave1Indices { get; }
    public int TrineWave2Index { get; }
    public int[] TrineWave3OuterIndices { get; }
    public bool TrineCenterIsRight { get; }

    public static readonly (Vector2 Center, bool IsRight)[] OuterTrines =
    [
        (new Vector2(2.89f, -15.00f), true),
        (new Vector2(14.43f, -5.00f), false),
        (new Vector2(11.55f, 10.00f), true),
        (new Vector2(-2.89f, 15.00f), false),
        (new Vector2(-14.43f, 5.00f), true),
        (new Vector2(-11.55f, -10.00f), false),
    ];

    public UmadP2FullState(Rng rng, SimParty party, UmadP2FullStateOverrides overrides, float countdownSeconds = 5.0f)
    {
        CountdownSeconds = countdownSeconds;
        Forsaken = new UmadP2ForsakenState(rng, party, overrides.Forsaken);
        FirstWingsEastUnsafe = overrides.FirstWingsEastUnsafe ?? rng.NextBool();
        TrineCenterIsRight = overrides.TrineCenterIsRight ?? rng.NextBool();

        var order = rng.Shuffle(Enumerable.Range(0, OuterTrines.Length).ToArray()).ToArray();
        TrineWave1Indices = order[..3].OrderBy(i => i).ToArray();
        TrineWave2Index = order[3];
        TrineWave3OuterIndices = order[4..6].OrderBy(i => i).ToArray();
    }

    public static Vector2[] GetTrineVertices(Vector2 center, bool isRight)
    {
        const float r = 5.7735f;
        const float halfR = 2.8868f;
        const float halfSide = 5.0f;
        return isRight
            ? [new(center.X + r, center.Y), new(center.X - halfR, center.Y - halfSide), new(center.X - halfR, center.Y + halfSide)]
            : [new(center.X - r, center.Y), new(center.X + halfR, center.Y - halfSide), new(center.X + halfR, center.Y + halfSide)];
    }

    public (int PartyIndex, int TankIndex) PickTrineSafePair()
        => (TrineWave1Indices[0], TrineWave1Indices[^1]);

    public static IReadOnlyList<Vector2> GetWaveVertices(IReadOnlyList<int> outerIndices, bool includeCenter, bool centerIsRight)
    {
        var list = new List<Vector2>((outerIndices.Count + (includeCenter ? 1 : 0)) * 3);
        if (includeCenter)
            list.AddRange(GetTrineVertices(Vector2.Zero, centerIsRight));
        for (var i = 0; i < outerIndices.Count; i++)
        {
            var (center, isRight) = OuterTrines[outerIndices[i]];
            list.AddRange(GetTrineVertices(center, isRight));
        }
        return list;
    }
}