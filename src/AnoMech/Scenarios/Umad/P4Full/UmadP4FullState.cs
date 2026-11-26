using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P4KefkaSays;

namespace AnoMech.Scenarios.Umad.P4Full;

public sealed class UmadP4FullState
{
    public float CountdownSeconds { get; }
    public bool EnableCheatSheet { get; }
    public UmadP4KefkaSaysState KefkaSays { get; }

    public UmadP4FullState(SimWorld world, UmadP4FullStateOverrides overrides, float countdownSeconds)
    {
        CountdownSeconds = countdownSeconds;
        EnableCheatSheet = overrides.EnableCheatSheet;
        KefkaSays = new UmadP4KefkaSaysState(world.Rng, world.Party, overrides.KefkaSays);
    }
}
