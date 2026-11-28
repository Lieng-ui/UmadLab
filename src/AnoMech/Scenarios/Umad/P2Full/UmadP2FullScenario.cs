using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P2Forsaken;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Full;

public sealed class UmadP2FullScenario : IScenario
{
    public string Name => "Full";
    public IPhase Phase => UmadZone.P2;
    public float BgmSecondsAtStart => 0f;
    public float BgmStartDelaySeconds => state?.CountdownSeconds ?? 0f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP2FullAi()];
    public object SettingsOverrides => settingsWindow.Overrides;

    public const float ForsakenOffset = 18.0f;
    public static bool SuppressNextCountdown;

    private static readonly (uint State, uint Bitmask) EObjAnimAppear = (0x0001, 0x0002);
    private static readonly (uint State, uint Bitmask) EObjAnimFall = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimLand = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimExplode = (0x0004, 0x0008);

    private readonly UmadP2FullSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP2FullState state = null!;
    private SimEnemy? kefka;
    private SimEnemy? kefkaHuman;
    private IReadOnlyList<SimCharacter> endAttackTargets = [];
    private int towerRound;

    private readonly List<SimEventObject> trineWave1Props = [];
    private readonly List<SimEventObject> trineWave2Props = [];
    private readonly List<SimEventObject> trineWave3Props = [];
    private int trinePropsPlaced;

    public UmadP2FullState? LastState { get; private set; }

    public void DrawSettings() => settingsWindow.Draw();

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
    }

    private void At(float fightTime, Action action)
        => world.Events.Add(state.CountdownSeconds + fightTime, action);

    private void AtForsaken(float forsakenTime, Action action)
        => At(ForsakenOffset + forsakenTime, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        var suppressCd = SuppressNextCountdown;
        SuppressNextCountdown = false;
        state = new UmadP2FullState(world.Rng, party, settingsWindow.Overrides, suppressCd ? 0f : settingsWindow.Overrides.CountdownSeconds);
        LastState = state;
        kefkaHuman = null;
        endAttackTargets = [];
        towerRound = 0;
        trinePropsPlaced = 0;
        ClearTrineProps();

        ScheduleCountdownLogs();

        if (selectedAi is { } idx && idx >= 0 && idx < AiStrats.Count)
            ((IScenarioAi<UmadP2FullState>)AiStrats[idx]).Run(state, world);

        SpawnActors();
        SchedulePreForsaken();
        ScheduleForsaken();
        ScheduleTrineAndWings();
    }

    public void Tick(float delta, float elapsed)
    {
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        Towers.ResolveTower(world, resolved, ActionId.ThePathOfLight, UmadActions.TheRiverOfLight, minSoakers: 2);
        ResolveForsakenSoakers(resolved);
    }

    private void ScheduleCountdownLogs()
    {
        var cd = state.CountdownSeconds;
        if (cd <= 0f) return;
        for (var sec = (int)cd; sec >= 1; sec--)
        {
            var remaining = sec;
            world.Events.Add(cd - sec, () => Plugin.ChatGui?.Print($"距离战斗开始还有 {remaining} 秒..."));
        }
        world.Events.Add(cd, () => Plugin.ChatGui?.Print("战斗开始！"));
    }

    private void SpawnActors()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.GodKefka,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            LayoutId: BNpcLayoutId.GodKefka,
            Placement: new Placement(Vector3.Zero, -MathF.PI)));

        for (var i = 0; i < 3; i++)
        {
            var clone = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaClone,
                NameId: BNpcNameId.Kefka,
                Level: 100,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.HiddenUntilShown,
                LayoutId: BNpcLayoutId.KefkaClone,
                Placement: new Placement(Vector3.Zero, 0f)));
            ScheduleCloneEndAttack(clone, 32.35f, 0, i);
            ScheduleCloneEndAttack(clone, 53.59f, 1, i);
            ScheduleCloneEndAttack(clone, 74.61f, 2, i);
            ScheduleCloneEndAttack(clone, 95.51f, 3, i);
        }
    }

    private void SchedulePreForsaken()
    {
        foreach (var at in new[] { 3.50f, 6.50f, 15.00f, 18.00f })
            At(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));

        At(7.23f, () =>
        {
            if (party.Get(PartyRole.MainTank) is not { } mt) return;
            mt.AttachLockonVfx(LockonId.TankBuster);
            kefka?.Cast(UmadActions.UltimateEmbrace, mt);
        });
    }

    private void ScheduleForsaken()
    {
        var fs = state.Forsaken;

        foreach (var at in new[] { 1.30f, 11.35f, 14.39f, 17.43f, 20.47f, 24.50f })
            AtForsaken(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.OffTank)));

        AtForsaken(2.37f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: 0x11));
        AtForsaken(2.46f, () => kefka?.Cast(UmadActions.Forsaken));
        AtForsaken(9.43f, () => world.Map.AddEffect(packetFlags: 0x00020001U, index: 0x11));
        AtForsaken(10.38f, () => world.Map.SetWeather(89, transition: 2f));
        AtForsaken(11.04f, () =>
        {
            world.Map.AddEffect(packetFlags: 0x00040001U, index: 0x12);
            party.ForEachActive(p => p.AddStatus(StatusId.SpellsTrouble, duration: 999f, stacks: 4, overrideStacks: true));
            party.ForEachActive(p => p.AddStatus(StatusId.Unknown13DC));
            party.ForEachActive(c => c.AttachLockonVfx(fs.Lockons[((ISimPartyMember)c).Role]));
        });

        float[] towerVisualStarts = [12.51f, 22.53f, 33.56f, 43.58f, 54.61f, 64.63f, 75.67f, 85.68f];
        float[] towerResolveStarts = [22.66f, 32.67f, 43.71f, 53.72f, 64.76f, 74.77f, 85.81f, 95.82f];
        for (var i = 0; i < UmadP2ForsakenState.TowerRounds; i++)
        {
            var (d1, d2) = fs.GetTowers(i);
            ScheduleTowerVisual(towerVisualStarts[i], d1);
            ScheduleTowerVisual(towerVisualStarts[i], d2);
            ScheduleTowerResolve(towerResolveStarts[i], i);
        }

        ScheduleKefkaEndAttack(0, 25.61f);
        ScheduleKefkaEndAttack(1, 46.75f);
        ScheduleKefkaEndAttack(2, 67.76f);
        ScheduleKefkaEndAttack(3, 88.81f);

        AtForsaken(109.47f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.OffTank)));
        AtForsaken(110.84f, () => kefka?.Cast(UmadActions.LightOfJudgment));
        AtForsaken(116.40f, () => world.Map.SetWeather(79, transition: 0f));
    }

    private void ScheduleTowerVisual(float forsakenTime, Direction direction)
    {
        var idx = (byte)(direction.Index() + 1);
        AtForsaken(forsakenTime, () => world.Map.AddEffect(packetFlags: 0x00020001U, index: idx));
        AtForsaken(forsakenTime + 3f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: idx));
    }

    private void ScheduleTowerResolve(float forsakenTime, int index)
    {
        AtForsaken(forsakenTime, () =>
        {
            towerRound = index;
            kefka?.CastFromHelper(UmadActions.ThePathOfLight, at: TowerSpot(index, 0));
            kefka?.CastFromHelper(UmadActions.ThePathOfLight, at: TowerSpot(index, 1));
        });
    }

    private Placement TowerSpot(int round, int tower)
    {
        var (d1, d2) = state.Forsaken.GetTowers(round);
        return (tower == 0 ? d1 : d2).Apply(new Placement(new Vector3(0f, 0f, -8f), 0f));
    }

    private void ScheduleKefkaEndAttack(int index, float forsakenStart)
    {
        var end = state.Forsaken.EndAttacks[index];
        AtForsaken(forsakenStart, () => kefka?.Cast(end.CastBarAction, animationLock: 0.2f));
        AtForsaken(forsakenStart + 6.7f, () =>
        {
            if (kefka == null) return;
            endAttackTargets = party.Find.ClosestN(kefka.Position, 4);
            if (endAttackTargets.Count > 0) kefka.Cast(end.KefkaHit, endAttackTargets[0]);
        });
        AtForsaken(forsakenStart + 12.5f, () => kefka?.Face(party.Player));
        AtForsaken(forsakenStart + 12.9f, () => kefka?.Cast(end.AllThingsEnding));
        AtForsaken(forsakenStart + 21.0f, () => kefka?.Face(party.Get(PartyRole.OffTank)));
    }

    private void ScheduleCloneEndAttack(SimEnemy? clone, float forsakenStart, int number, int index)
    {
        var end = state.Forsaken.EndAttacks[number];
        AtForsaken(forsakenStart - 0.5f, () => clone?.SetPosition(Vector3.Zero));
        AtForsaken(forsakenStart, () =>
        {
            if (endAttackTargets.Count > index + 1) clone?.Cast(end.CloneHit, endAttackTargets[index + 1]);
        });
        AtForsaken(forsakenStart + 5.7f, () => clone?.Face(party.Player));
        AtForsaken(forsakenStart + 6.1f, () => clone?.Cast(end.AllThingsEnding));
        AtForsaken(forsakenStart + 14.1f, () => clone?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void ResolveForsakenSoakers(ActionResolved resolved)
    {
        if (resolved.Action.ActionId != ActionId.ThePathOfLight || resolved.Hits.Count < 2) return;
        var tower = Vector3.DistanceSquared(resolved.Origin.Position, TowerSpot(towerRound, 0).Position) < 1f ? 0 : 1;
        for (var i = 0; i < resolved.Hits.Count; i++) ResolveForsakenLockonEffect(resolved.Hits[i]);
        var lockons = state.Forsaken.TowerLockons[towerRound * 2 + tower];
        foreach (var (soaker, lockon) in resolved.Hits.OrderBy(h => ((ISimPartyMember)h).Role).Zip(lockons))
            world.Events.Add(0.6f, () => ReassignForsakenLockon(soaker, lockon));
    }

    private void ReassignForsakenLockon(SimCharacter soaker, uint lockon)
    {
        var role = ((ISimPartyMember)soaker).Role;
        soaker.AddStatus(StatusId.SpellsTrouble, duration: 999f, stacks: -1);
        state.Forsaken.Lockons[role] = soaker.HasStatus(StatusId.SpellsTrouble) ? lockon : 0;
        if (state.Forsaken.Lockons[role] != 0) soaker.AttachLockonVfx(lockon);
    }

    private void ResolveForsakenLockonEffect(SimCharacter character)
    {
        var lockon = state.Forsaken.Lockons[((ISimPartyMember)character).Role];
        world.Events.Add(0.6f, () =>
        {
            switch (lockon)
            {
                case LockonId.ForsakenStack:
                    kefka?.CastFromHelper(UmadActions.Spelldriver, character);
                    break;
                case LockonId.ForsakenChariot:
                    kefka?.CastFromHelper(UmadActions.Spellscatter, character);
                    break;
                case LockonId.ForsakenCone:
                    var position = character.Placement();
                    var closest = party.Find.Closest(character.Position, character);
                    if (closest != null) position = position.Face(closest.Position);
                    kefka?.CastFromHelper(UmadActions.Spellwave, at: position);
                    break;
            }
        });
    }

    private void ScheduleTrineAndWings()
    {
        foreach (var at in new[] { 135.80f, 138.80f, 141.80f, 147.80f, 154.90f })
            At(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));

        At(142.08f, () =>
        {
            kefka?.SetPosition(new Placement(Vector3.Zero, -MathF.PI));
            kefka?.Cast(ActionId.TrineCast, animationLock: 2.1f);
        });

        At(145.85f, () => SpawnTrineWave(state.TrineWave1Indices, includeCenter: false, trineWave1Props));
        At(147.86f, () => SpawnTrineWave([state.TrineWave2Index], includeCenter: false, trineWave2Props));

        At(148.22f, () =>
        {
            kefka?.SetPosition(new Placement(Vector3.Zero, -MathF.PI));
            kefka?.Cast(state.FirstWingsEastUnsafe ? UmadActions.WingsOfDestructionEast : UmadActions.WingsOfDestructionWest);
        });

        At(149.86f, () => SpawnTrineWave(state.TrineWave3OuterIndices, includeCenter: true, trineWave3Props));

        At(157.95f, () => ResolveTrineWave(state.TrineWave1Indices, includeCenter: false, trineWave1Props));
        At(158.38f, () => kefka?.Cast(ActionId.WingsOfDestructionDoubleCast, animationLock: 2.1f));
        At(159.95f, () => ResolveTrineWave([state.TrineWave2Index], includeCenter: false, trineWave2Props));
        At(161.96f, () => ResolveTrineWave(state.TrineWave3OuterIndices, includeCenter: true, trineWave3Props));
        At(162.45f, ResolveDoubleWings);

        At(164.48f, () =>
        {
            if (party.Get(PartyRole.MainTank) is not { } mt) return;
            mt.AttachLockonVfx(LockonId.TankBuster);
            kefka?.Cast(UmadActions.UltimateEmbrace, mt);
        });

        At(166.26f, ClearTrineProps);
        At(172.80f, () =>
        {
            kefka?.SetTargetable(false);
            kefka?.PlayActionTimeline(ActionTimelineId.WarpStart);
            kefkaHuman = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaP3,
                NameId: BNpcNameId.Kefka,
                Level: 100,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.Visible,
                LayoutId: BNpcLayoutId.KefkaP3,
                Placement: new Placement(Vector3.Zero, 0f)));
        });
        At(173.60f, () => kefka?.Despawn());
        At(173.83f, () => kefkaHuman?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        At(175.97f, () => kefkaHuman?.Cast(UmadActions.HeartlessArchangelStorm));
        At(179.71f, () => world.Map.AddEffect(packetFlags: 0x04000200U, index: 0x00));
        At(179.80f, () => party.ForEachActive(p => p.AddStatus(StatusId.DownForTheCount, 3f)));
        At(182.80f, () => { });
    }

    private void SpawnTrineWave(IReadOnlyList<int> outerIndices, bool includeCenter, List<SimEventObject> bucket)
    {
        var delay = 0f;
        for (var i = 0; i < outerIndices.Count; i++)
        {
            var (center, isRight) = UmadP2FullState.OuterTrines[outerIndices[i]];
            var d = delay;
            if (d <= 0f)
                SpawnTrineProp(center, isRight, bucket);
            else
                world.Events.Add(d, () => SpawnTrineProp(center, isRight, bucket));
            delay += 0.15f;
        }
        if (includeCenter)
        {
            var d = delay;
            if (d <= 0f)
                SpawnTrineProp(Vector2.Zero, state.TrineCenterIsRight, bucket);
            else
                world.Events.Add(d, () => SpawnTrineProp(Vector2.Zero, state.TrineCenterIsRight, bucket));
        }
    }

    private void SpawnTrineProp(Vector2 center, bool isRight, List<SimEventObject> bucket)
    {
        var propEventId = settingsWindow.Overrides.PropsBindDirector ? EObjId.PropEventId : 0u;
        var prop = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = isRight ? EObjId.TrineRight : EObjId.TrineLeft,
            Placement = new Placement(new Vector3(center.X, 0f, center.Y), 0f),
            TargetableStatus = 5,
            EventId = propEventId,
            EntityId = Spawn.TelePortentEntityIdBase + (uint)trinePropsPlaced,
            Arg2 = ((uint)(Spawn.TelePortentArg2Serial + trinePropsPlaced) << 16) | 0x3U,
            TimelineState = 0x0001,
        });
        if (prop != null)
        {
            prop.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            world.Events.Add(0.10f, () => { if (prop.IsAlive) prop.PlayBeat(EObjAnimFall.State, EObjAnimFall.Bitmask); });
            world.Events.Add(1.60f, () => { if (prop.IsAlive) prop.PlayBeat(EObjAnimLand.State, EObjAnimLand.Bitmask); });
            bucket.Add(prop);
        }
        trinePropsPlaced++;
    }

    private void ResolveTrineWave(IReadOnlyList<int> outerIndices, bool includeCenter, List<SimEventObject> bucket)
    {
        var exploding = new List<SimEventObject>(bucket);
        bucket.Clear();
        for (var i = 0; i < exploding.Count; i++)
            if (exploding[i].IsAlive) exploding[i].PlayBeat(EObjAnimExplode.State, EObjAnimExplode.Bitmask);
        world.Events.Add(1.0f, () => DespawnProps(exploding));
        var vertices = UmadP2FullState.GetWaveVertices(outerIndices, includeCenter, state.TrineCenterIsRight);
        for (var i = 0; i < vertices.Count; i++)
            kefka?.CastFromHelper(UmadActions.TrineResolve, at: new Placement(new Vector3(vertices[i].X, 0f, vertices[i].Y), 0f));
    }

    private void ResolveDoubleWings()
    {
        var origin = kefka?.Position ?? Vector3.Zero;
        SimCharacter? closest = null;
        SimCharacter? farthest = null;
        var minDistSq = float.PositiveInfinity;
        var maxDistSq = -1f;
        foreach (var member in party.ActiveMembers())
        {
            if (!member.IsAlive()) continue;
            var dx = member.Position.X - origin.X;
            var dz = member.Position.Z - origin.Z;
            var dSq = dx * dx + dz * dz;
            if (dSq < minDistSq) { minDistSq = dSq; closest = member; }
            if (dSq > maxDistSq) { maxDistSq = dSq; farthest = member; }
        }
        if (closest != null) kefka?.CastFromHelper(UmadActions.WingsOfDestructionDoubleHit, closest);
        if (farthest != null && farthest != closest) kefka?.CastFromHelper(UmadActions.WingsOfDestructionDoubleHit, farthest);
    }

    private void ClearTrineProps()
    {
        DespawnProps(trineWave1Props);
        DespawnProps(trineWave2Props);
        DespawnProps(trineWave3Props);
    }

    private static void DespawnProps(List<SimEventObject> props)
    {
        for (var i = 0; i < props.Count; i++)
            if (props[i].IsAlive) props[i].Despawn();
        props.Clear();
    }
}