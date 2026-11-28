using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P1GravenImage1;
using AnoMech.Scenarios.Umad.P1GravenImage2;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;

namespace AnoMech.Scenarios.Umad.P1Full;

public sealed class UmadP1FullState
{
    public float CountdownSeconds { get; }
    public UmadP1GravenImage1State Gi1 { get; }
    public UmadP1GravenImage2State Gi2 { get; }
    public UmadP1TeleTrouncingState Gi3 { get; }

    public UmadP1FullState(Rng rng, UmadP1FullStateOverrides overrides)
    {
        CountdownSeconds = overrides.CountdownSeconds < 0f ? 0f : overrides.CountdownSeconds;
        Gi1 = new UmadP1GravenImage1State(rng, new UmadP1GravenImage1StateOverrides
        {
            CountdownSeconds = 0f,
            PropsBindDirector = overrides.PropsBindDirector,
        });
        var trap2Support = PickDifferent(rng, UmadP1GravenImage2State.SupportRoles, Gi1.Trap1Support);
        var trap2Dps = PickDifferent(rng, UmadP1GravenImage2State.DpsRoles, Gi1.Trap1Dps);
        Gi2 = new UmadP1GravenImage2State(rng, new UmadP1GravenImage2StateOverrides
        {
            CountdownSeconds = 0f,
            PropsBindDirector = overrides.PropsBindDirector,
            Trap2Support = trap2Support,
            Trap2Dps = trap2Dps,
        });
        var trap3Support = PickDifferent(rng, UmadP1GravenImage2State.SupportRoles, Gi2.Trap2Support);
        var trap3Dps = PickDifferent(rng, UmadP1GravenImage2State.DpsRoles, Gi2.Trap2Dps);
        Gi3 = new UmadP1TeleTrouncingState(rng, new UmadP1TeleTrouncingStateOverrides
        {
            ConfettiStackSupport = trap3Support,
            ConfettiStackDps = trap3Dps,
        });
    }

    private static PartyRole PickDifferent(Rng rng, PartyRole[] pool, PartyRole excluded)
    {
        var candidates = pool.Where(r => r != excluded).ToArray();
        return candidates[rng.NextInt(candidates.Length)];
    }
}
