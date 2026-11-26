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
        Gi2 = new UmadP1GravenImage2State(rng, new UmadP1GravenImage2StateOverrides
        {
            CountdownSeconds = 0f,
            PropsBindDirector = overrides.PropsBindDirector,
        });
        Gi3 = new UmadP1TeleTrouncingState(rng, new UmadP1TeleTrouncingStateOverrides());
    }
}
