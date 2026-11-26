using System.Collections.Generic;
using System.Linq;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.FullEncounter;
using AnoMech.Scenarios.Umad.P2Forsaken;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Scenarios.Umad.P3LimitCut;
using AnoMech.Scenarios.Umad.P4KefkaSays;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;

namespace AnoMech.Scenarios;

// Every runnable scenario, in menu order, plus the zone -> phase -> scenario tree derived from it in
// first-appearance order. Fresh instances per Create: a scenario holds its run state.
public sealed class ScenarioCatalog
{
    public IReadOnlyList<IScenario> Scenarios { get; }
    public IReadOnlyList<IZone> Zones { get; }
    private readonly Dictionary<IZone, IReadOnlyList<IPhase>> phasesByZone;
    private readonly Dictionary<IPhase, IReadOnlyList<IScenario>> scenariosByPhase;

    private ScenarioCatalog(IReadOnlyList<IScenario> scenarios)
    {
        Scenarios = scenarios;
        var zones = new List<IZone>();
        var phases = new Dictionary<IZone, List<IPhase>>();
        var byPhase = new Dictionary<IPhase, List<IScenario>>();
        foreach (var scenario in scenarios)
        {
            var phase = scenario.Phase;
            var zone = phase.Zone;
            if (!phases.TryGetValue(zone, out var zonePhases))
            {
                zonePhases = [];
                phases[zone] = zonePhases;
                zones.Add(zone);
            }
            if (!zonePhases.Contains(phase)) zonePhases.Add(phase);
            if (!byPhase.TryGetValue(phase, out var phaseScenarios))
            {
                phaseScenarios = [];
                byPhase[phase] = phaseScenarios;
            }
            phaseScenarios.Add(scenario);
        }
        Zones = zones;
        phasesByZone = phases.ToDictionary(kv => kv.Key, IReadOnlyList<IPhase> (kv) => kv.Value);
        scenariosByPhase = byPhase.ToDictionary(kv => kv.Key, IReadOnlyList<IScenario> (kv) => kv.Value);
    }

    public IReadOnlyList<IPhase> PhasesOf(IZone zone) => phasesByZone[zone];
    public IReadOnlyList<IScenario> ScenariosOf(IPhase phase) => scenariosByPhase[phase];

    public static ScenarioCatalog Create() => new(
    [
        new AnoMech.Scenarios.Umad.P1Full.UmadP1FullScenario(),
        new AnoMech.Scenarios.Umad.P2Full.UmadP2FullScenario(),
        new AnoMech.Scenarios.Umad.P3Full.UmadP3FullScenario(),
        new AnoMech.Scenarios.Umad.P4Full.UmadP4FullScenario(),
        new AnoMech.Scenarios.Umad.P5Full.UmadP5FullScenario(),
    ]);
}
