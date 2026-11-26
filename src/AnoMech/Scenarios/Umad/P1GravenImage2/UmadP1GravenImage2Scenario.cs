using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P1GravenImage2;

using Constants = UmadConstants;

public sealed class UmadP1GravenImage2Scenario : IScenario
{
    public string Name => "Graven Image 2 (78-147s)";
    public IPhase Phase => UmadZone.P1;
    public float BgmSecondsAtStart => 78f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP1GravenImage2Ai()];
    public object SettingsOverrides => settingsWindow.Overrides;

    private sealed class PuddleEntry(SimEventObject prop, Vector2 pos, float armedAt)
    {
        public SimEventObject Prop { get; } = prop;
        public Vector2 Pos { get; } = pos;
        public float ArmedAt { get; } = armedAt;
        public float? TriggeredAt { get; set; }
        public bool Resolved { get; set; }
    }

    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP1GravenImage2State state = null!;
    private SimEnemy? kefka;
    private readonly List<SimEnemy> gravityHelpers = [];
    private readonly List<SimEnemy> rockHelpers = [];
    private SimEnemy? cleaveHelper;
    private SimEventObject? statuePropA1;
    private SimEventObject? statuePropA2;
    private SimEventObject? statuePropGravity;
    private SimEventObject? statuePropRock;
    private SimEventObject? statuePropCleaveWest;
    private SimEventObject? statuePropCleaveEast;
    private SimEventObject? colossusProp;
    private readonly List<PuddleEntry> r1Puddles = [];
    private readonly List<PuddleEntry> r2Puddles = [];
    private int puddlesPlaced;
    private bool puddlesClearable;
    private bool wiped;
    private readonly UmadP1GravenImage2SettingsWindow settingsWindow = new();

    internal static readonly Vector3 GravityStatuePos = new(2.5f, 22.5f, -73.0f);
    internal static readonly Vector3 RockStatuePos = new(26.0f, 7.0f, -58.5f);
    private static readonly Vector3 CleaveWestStatuePos = new(-8.0f, 15.0f, -73.0f);
    private static readonly Vector3 CleaveEastStatuePos = new(16.0f, 6.5f, -57.0f);

    private const ushort ColossusStage1State = 0x0001;
    private const float PuddleArmGrace = 1.65f;
    private const float PuddleTouchRadius = 2.2f;
    private const float PuddleClearRadius = 3.2f;
    private const float PuddleClearFuse = 1.25f;
    private const float RockBulletRadius = 6.0f;

    private static readonly (uint State, uint Bitmask) EObjAnimAppear = (0x0001, 0x0002);
    private static readonly (uint State, uint Bitmask) EObjAnimTetherFire = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimCleaveWindUp = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimCleaveResolve = (0x0100, 0x0200);
    private static readonly (uint State, uint Bitmask) EObjAnimDespawn = (0x0004, 0x0008);

    private static readonly EnemyAction GravityPuddleWipe = UmadActions.UnmitigatedImpact with
    {
        DeathExplanation = "stepped into a gravity puddle before it became clearable",
    };

    private static readonly EnemyAction RockBulletWipe = UmadActions.UnmitigatedImpact with
    {
        DeathExplanation = "Rock Bullet hit a gravity puddle or another player",
    };

    // Cone headings in FFXIV/SimCast convention (south=0, +pi/4=SE, +3pi/4=SW, -3pi/4=NW, -pi/4=NE).
    private static readonly float[] IcePairA = [-MathF.PI / 4f, 3f * MathF.PI / 4f];
    private static readonly float[] IcePairB = [-3f * MathF.PI / 4f, MathF.PI / 4f];

    public UmadP1GravenImage2State? LastState { get; private set; }

    public void DrawSettings() => settingsWindow.Draw();

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
    }

    private void At(float fightTime, Action action)
        => world.Events.Add(state.CountdownSeconds + fightTime, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UmadP1GravenImage2State(world.Rng, settingsWindow.Overrides);
        LastState = state;

        CleanupObjects();

        world.Events.Add(0f, SpawnInitialActors);
        ScheduleCountdownLogs();

        At(0.05f, ApplyInitialTrap2Debuffs);

        foreach (var at in new[] { 2.50f, 5.50f, 26.50f, 36.00f, 49.00f, 65.50f })
            At(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank), animationVariation: 1));

        // Graven Image 2 cast + colossus rises to stage 2 + statue props appear.
        At(0.45f, () =>
        {
            kefka?.Cast(Constants.ActionId.GravenImage, animationLock: 2.1f);
            colossusProp?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
            statuePropGravity?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            statuePropRock?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
        });

        // Round 1: 8 tethers (4 Gravity + 4 Rock) + Single Ice + Gravity 1 (outer N/S) + Rock 1 (spread).
        At(4.19f, () => TetherGravityAndRock(round: 1, state.R1DpsGetsGravity));
        At(5.48f, () => kefka?.AttachLockonVfx(state.IceIsLie ? Constants.LockonId.ColdFalse : Constants.LockonId.ColdTrue));
        At(5.57f, () =>
        {
            kefka?.Cast(Constants.ActionId.BlizzardIIIBlowoutCast, animationLock: 3.1f);
            SpawnIceCones(state.IceDiagonalB, state.IceIsLie);
        });
        At(10.69f, () => ResolveGravityBullets(round: 1, state.R1DpsGetsGravity));
        At(11.29f, () => SpawnGravityPuddles(round: 1, state.R1GravityDrops, spawnFightTime: 11.29f));
        At(14.71f, () => ResolveRockBullets(state.R1DpsGetsGravity));

        // Tankbuster 2: Vicious Destruction (2-hit) on ST.
        At(15.47f, () =>
        {
            if (party.Get(PartyRole.OffTank) is not { } st) return;
            st.AttachLockonVfx(Constants.LockonId.TankBuster);
            kefka?.Cast(UmadActions.ViciousDestruction1, st);
        });
        At(23.71f, () =>
        {
            if (party.Get(PartyRole.OffTank) is { } st)
                kefka?.Cast(UmadActions.ViciousDestruction2, st);
        });

        // Round 2: 8 tethers + Half-Room Cleave 1 + Gravity 2 (inner N/S) + Rock 2 (spread) + Half-Room Cleave 2.
        At(20.64f, () => TetherGravityAndRock(round: 2, state.R2DpsGetsGravity));
        At(21.00f, () => TelegraphHalfRoomCleave(index: 1, state.Cleave1EastUnsafe));
        At(24.51f, () => ResolveHalfRoomCleave(state.Cleave1EastUnsafe));
        At(29.14f, () => ResolveGravityBullets(round: 2, state.R2DpsGetsGravity));
        At(29.69f, () => SpawnGravityPuddles(round: 2, state.R2GravityDrops, spawnFightTime: 29.69f));
        At(33.15f, () => ResolveRockBullets(state.R2DpsGetsGravity));
        At(34.20f, () => TelegraphHalfRoomCleave(index: 2, state.Cleave2EastUnsafe));
        At(37.70f, () => ResolveHalfRoomCleave(state.Cleave2EastUnsafe));

        // Puddles become stack-clearable -> Trap 2 stack (41.49s) -> Heavy Gravity Wave 1 (R2) -> Wave 2 (R1).
        At(41.10f, ActivateGravityPuddles);
        At(41.49f, ResolveTrap2Stacks);
        At(45.80f, () => ResolveHeavyGravityWave(wave: 1, state.R2GravityDrops, r2Puddles));
        At(47.10f, () => DespawnPuddles(r2Puddles));
        At(49.20f, () => ResolveHeavyGravityWave(wave: 2, state.R1GravityDrops, r1Puddles));
        At(50.20f, () => DespawnPuddles(r1Puddles));

        // Light of Judgment raidwide + 3x Overdrive on MT.
        At(50.67f, () => kefka?.Cast(UmadActions.LightOfJudgmentP1));
        foreach (var at in new[] { 58.82f, 60.91f, 63.01f })
            At(at, () =>
            {
                if (party.Get(PartyRole.MainTank) is { } mt)
                    kefka?.Cast(UmadActions.Overdrive, mt);
            });

        // Empty tail event at 68.00s so ScenarioSession finishes cleanly at 69.00s.
        At(68.00f, () => { });

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP1GravenImage2State>)AiStrats[idx]).Run(state, world);
    }

    public void Tick(float delta, float elapsed)
    {
        if (wiped) return;
        if (!puddlesClearable)
        {
            CheckPuddleTouch(r1Puddles, elapsed);
            if (!wiped) CheckPuddleTouch(r2Puddles, elapsed);
        }
        else
        {
            TickClearablePuddles(r2Puddles, elapsed);
            TickClearablePuddles(r1Puddles, elapsed);
        }
    }

    private void TickClearablePuddles(List<PuddleEntry> puddles, float elapsed)
    {
        const float clearRadiusSq = PuddleClearRadius * PuddleClearRadius;
        for (var i = 0; i < puddles.Count; i++)
        {
            var entry = puddles[i];
            if (entry.Resolved || !entry.Prop.IsAlive) continue;
            if (entry.TriggeredAt is null)
            {
                foreach (var role in UmadP1GravenImage2State.AllRoles)
                {
                    if (party.Get(role) is not { } member || !member.IsAlive()) continue;
                    var dx = member.Position.X - entry.Pos.X;
                    var dz = member.Position.Z - entry.Pos.Y;
                    if (dx * dx + dz * dz <= clearRadiusSq)
                    {
                        entry.TriggeredAt = elapsed;
                        break;
                    }
                }
            }
            else if (elapsed - entry.TriggeredAt.Value >= PuddleClearFuse)
            {
                entry.Resolved = true;
                entry.Prop.PlayBeat(EObjAnimDespawn.State, EObjAnimDespawn.Bitmask);
                kefka?.CastFromHelper(UmadActions.HeavyGravity, at: new Placement(new Vector3(entry.Pos.X, 0f, entry.Pos.Y), 0f));
            }
        }
    }

    private void CheckPuddleTouch(List<PuddleEntry> puddles, float elapsed)
    {
        const float touchRadiusSq = PuddleTouchRadius * PuddleTouchRadius;
        for (var i = 0; i < puddles.Count; i++)
        {
            var entry = puddles[i];
            if (elapsed < entry.ArmedAt || !entry.Prop.IsAlive) continue;
            foreach (var role in UmadP1GravenImage2State.AllRoles)
            {
                if (party.Get(role) is not { } member || !member.IsAlive()) continue;
                var dx = member.Position.X - entry.Pos.X;
                var dz = member.Position.Z - entry.Pos.Y;
                if (dx * dx + dz * dz <= touchRadiusSq)
                {
                    TriggerWipe(
                        GravityPuddleWipe,
                        new Vector3(entry.Pos.X, 0f, entry.Pos.Y),
                        $"{role} 在黑泥可消除前踩中了黑泥");
                    return;
                }
            }
        }
    }

    private void TriggerWipe(EnemyAction wipeAction, Vector3 origin, string reason)
    {
        if (wiped) return;
        wiped = true;
        DiagnosticLog.Warn($"[UmadP1GI2] Wipe triggered: {reason}");
        kefka?.CastFromHelper(wipeAction, at: new Placement(origin, 0f));
        world.Fail(reason);
    }

    private void ScheduleCountdownLogs()
    {
        var total = (int)MathF.Round(state.CountdownSeconds);
        if (total <= 0) return;
        for (var rem = total; rem >= 1; rem--)
        {
            if (rem == total || rem <= 5 || rem % 5 == 0)
            {
                var captured = rem;
                world.Events.Add(total - captured, () =>
                {
                    Plugin.ChatGui?.Print($"[绝妖星 P1二像] 战斗开始倒计时：{captured}秒");
                    DiagnosticLog.Info($"[UmadP1GI2] Pull countdown: {captured}s");
                });
            }
        }
        world.Events.Add(state.CountdownSeconds, () =>
        {
            Plugin.ChatGui?.Print("[绝妖星 P1二像] 战斗开始！");
            DiagnosticLog.Info("[UmadP1GI2] Pull countdown: ENGAGE!");
        });
    }

    private void CleanupObjects()
    {
        foreach (var h in gravityHelpers) h.Despawn();
        gravityHelpers.Clear();
        foreach (var h in rockHelpers) h.Despawn();
        rockHelpers.Clear();
        cleaveHelper?.Despawn();
        cleaveHelper = null;
        DespawnProp(ref statuePropA1);
        DespawnProp(ref statuePropA2);
        DespawnProp(ref statuePropGravity);
        DespawnProp(ref statuePropRock);
        DespawnProp(ref statuePropCleaveWest);
        DespawnProp(ref statuePropCleaveEast);
        DespawnProp(ref colossusProp);
        DespawnPuddles(r1Puddles);
        DespawnPuddles(r2Puddles);
        puddlesPlaced = 0;
        puddlesClearable = false;
        wiped = false;
        state.ClearGravityDrops();
    }

    private void DespawnProp(ref SimEventObject? prop)
    {
        if (prop == null) return;
        if (prop.IsAlive) prop.PlayBeat(EObjAnimDespawn.State, EObjAnimDespawn.Bitmask);
        prop.Despawn();
        prop = null;
    }

    private void DespawnPuddles(List<PuddleEntry> puddles)
    {
        foreach (var entry in puddles)
            entry.Prop.Despawn();
        puddles.Clear();
    }

    private void SpawnInitialActors()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.KefkaP3,
            NameId: Constants.BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, -float.Pi),
            LayoutId: Constants.BNpcLayoutId.KefkaP3));
        kefka?.KeepFacing(party.Get(PartyRole.MainTank));

        var propEventId = settingsWindow.Overrides.PropsBindDirector ? Constants.EObjId.PropEventId : 0u;
        statuePropA1 = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueA,
            LayoutId = Constants.EObjId.EarlierStatueA1LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase,
            TargetableStatus = 5,
            Arg2 = 0x00420003U,
            Placement = new Placement(new Vector3(0f, 18.5f, -44f), 0f),
            TimelineState = 0x0010,
        });
        statuePropA2 = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueA,
            LayoutId = Constants.EObjId.EarlierStatueA2LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 1u,
            TargetableStatus = 5,
            Arg2 = 0x00410003U,
            Placement = new Placement(new Vector3(0f, 0f, -35f), 0f),
            TimelineState = 0x0100,
        });
        statuePropGravity = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueB,
            LayoutId = Constants.EObjId.EarlierStatueB1LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 2u,
            TargetableStatus = 5,
            Arg2 = 0x00450003U,
            Placement = new Placement(GravityStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropRock = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueC,
            LayoutId = Constants.EObjId.EarlierStatueC1LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 3u,
            TargetableStatus = 5,
            Arg2 = 0x00460003U,
            Placement = new Placement(RockStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropCleaveWest = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueB,
            LayoutId = Constants.EObjId.EarlierStatueB2LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 4u,
            TargetableStatus = 5,
            Arg2 = 0x00430003U,
            Placement = new Placement(CleaveWestStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropCleaveEast = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueC,
            LayoutId = Constants.EObjId.EarlierStatueC2LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 5u,
            TargetableStatus = 5,
            Arg2 = 0x00440003U,
            Placement = new Placement(CleaveEastStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        colossusProp = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.GravenStatue,
            LayoutId = Constants.EObjId.GravenStatueLayoutId,
            EventId = propEventId,
            EntityId = 0x4000EB8Au,
            TargetableStatus = 5,
            Arg2 = Constants.EObjId.GravenStatueArg2,
            Placement = new Placement(Vector3.Zero, 0f),
            TimelineState = ColossusStage1State,
        });

        for (var i = 0; i < 4; i++)
        {
            if (SpawnGravenHelper(GravityStatuePos) is { } gh) gravityHelpers.Add(gh);
            if (SpawnGravenHelper(RockStatuePos) is { } rh) rockHelpers.Add(rh);
        }
        cleaveHelper = SpawnGravenHelper(Vector3.Zero);
    }

    private SimEnemy? SpawnGravenHelper(Vector3 position)
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.GravenImage,
            NameId: Constants.BNpcNameId.GravenImage,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(position, 0f),
            LayoutId: Constants.BNpcLayoutId.GravenImage));

    private void ApplyInitialTrap2Debuffs()
    {
        party.Get(state.Trap2Support)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 41.5f);
        party.Get(state.Trap2Dps)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 41.5f);
    }

    private void TetherGravityAndRock(int round, bool dpsGetsGravity)
    {
        var gravTargets = dpsGetsGravity ? UmadP1GravenImage2State.DpsRoles : UmadP1GravenImage2State.SupportRoles;
        var rockTargets = dpsGetsGravity ? UmadP1GravenImage2State.SupportRoles : UmadP1GravenImage2State.DpsRoles;
        var gravDur = round == 1 ? 6.50f : 8.50f;
        var rockDur = round == 1 ? 10.50f : 12.50f;

        for (var i = 0; i < 4; i++)
        {
            if (i < gravityHelpers.Count && party.Get(gravTargets[i]) is { } gm)
                world.Tether(gravityHelpers[i], gm, Constants.TetherId.GravenImage, duration: gravDur);
            if (i < rockHelpers.Count && party.Get(rockTargets[i]) is { } rm)
                world.Tether(rockHelpers[i], rm, Constants.TetherId.GravenImage, duration: rockDur);
        }

        var gravLabel = dpsGetsGravity ? "DPS" : "TN";
        var rockLabel = dpsGetsGravity ? "TN" : "DPS";
        DiagnosticLog.Info($"[UmadP1GI2] R{round} tethers: Gravity={gravLabel}, Rock={rockLabel}");
    }

    private void SpawnIceCones(bool diagonalB, bool isLie)
    {
        var realHeadings = diagonalB ? IcePairB : IcePairA;
        var fakeHeadings = diagonalB ? IcePairA : IcePairB;
        var realAction = isLie ? UmadActions.BlizzardBlowoutRealLie : UmadActions.BlizzardBlowoutRealTruth;

        foreach (var h in realHeadings)
            kefka?.CastFromHelper(realAction, at: new Placement(Vector3.Zero, h));

        if (!isLie) return;
        foreach (var h in fakeHeadings)
            kefka?.CastFromHelper(Constants.ActionId.BlizzardIIIBlowout_FakeOmen, at: new Placement(Vector3.Zero, h), animationLock: 1.1f);
    }

    private void ResolveGravityBullets(int round, bool dpsGetsGravity)
    {
        statuePropGravity?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        var targets = dpsGetsGravity ? UmadP1GravenImage2State.DpsRoles : UmadP1GravenImage2State.SupportRoles;
        for (var i = 0; i < targets.Length && i < gravityHelpers.Count; i++)
        {
            if (party.Get(targets[i]) is not { } member || !member.IsAlive()) continue;
            gravityHelpers[i].Cast(UmadActions.GravityBullet, member);
            var drop = new Vector2(member.Position.X, member.Position.Z);
            if (round == 1) state.RecordR1GravityDrop(drop);
            else state.RecordR2GravityDrop(drop);
            DiagnosticLog.Info($"[UmadP1GI2] R{round} GravityBullet -> {targets[i]} dropped at ({drop.X:F1},{drop.Y:F1})");
        }
    }

    private void SpawnGravityPuddles(int round, IReadOnlyList<Vector2> drops, float spawnFightTime)
    {
        var propEventId = settingsWindow.Overrides.PropsBindDirector ? Constants.EObjId.PropEventId : 0u;
        var list = round == 1 ? r1Puddles : r2Puddles;
        var armedAt = state.CountdownSeconds + spawnFightTime + PuddleArmGrace;
        for (var i = 0; i < drops.Count; i++)
        {
            var drop = drops[i];
            var puddle = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GravityPuddle,
                Placement = new Placement(new Vector3(drop.X, 0f, drop.Y), 0f),
                TargetableStatus = 5,
                EventId = propEventId,
                EntityId = Constants.Spawn.TelePortentEntityIdBase + (uint)puddlesPlaced,
                Arg2 = ((uint)(Constants.Spawn.TelePortentArg2Serial + puddlesPlaced) << 16) | 0x3U,
                TimelineState = 0x0001,
            });
            if (puddle != null)
            {
                puddle.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
                list.Add(new PuddleEntry(puddle, drop, armedAt));
            }
            puddlesPlaced++;
        }
        DiagnosticLog.Info($"[UmadP1GI2] Spawned {list.Count} R{round} gravity puddles (arms at fightTime={spawnFightTime + PuddleArmGrace:F2}s).");
    }

    private void ActivateGravityPuddles()
    {
        puddlesClearable = true;
        foreach (var entry in r1Puddles)
            if (entry.Prop.IsAlive) entry.Prop.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        foreach (var entry in r2Puddles)
            if (entry.Prop.IsAlive) entry.Prop.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
    }

    private void ResolveRockBullets(bool dpsGetsGravity)
    {
        statuePropRock?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        var targets = dpsGetsGravity ? UmadP1GravenImage2State.SupportRoles : UmadP1GravenImage2State.DpsRoles;
        const float rockRadiusSq = RockBulletRadius * RockBulletRadius;

        for (var i = 0; i < targets.Length && i < rockHelpers.Count; i++)
        {
            if (party.Get(targets[i]) is not { } member || !member.IsAlive()) continue;
            rockHelpers[i].Cast(UmadActions.RockBullet, member);

            if (wiped) continue;

            foreach (var otherRole in UmadP1GravenImage2State.AllRoles)
            {
                if (otherRole == targets[i]) continue;
                if (party.Get(otherRole) is not { } other || !other.IsAlive()) continue;
                var dx = other.Position.X - member.Position.X;
                var dz = other.Position.Z - member.Position.Z;
                if (dx * dx + dz * dz <= rockRadiusSq)
                {
                    TriggerWipe(RockBulletWipe, member.Position, $"{targets[i]} 的落石波及了 {otherRole}");
                    break;
                }
            }

            if (wiped) continue;

            if (RockHitsPuddle(member.Position, r1Puddles, rockRadiusSq)
                || RockHitsPuddle(member.Position, r2Puddles, rockRadiusSq))
            {
                TriggerWipe(RockBulletWipe, member.Position, $"{targets[i]} 的落石砸中了黑泥");
            }
        }
    }

    private static bool RockHitsPuddle(Vector3 rockCenter, List<PuddleEntry> puddles, float radiusSq)
    {
        for (var i = 0; i < puddles.Count; i++)
        {
            var entry = puddles[i];
            if (!entry.Prop.IsAlive) continue;
            var dx = rockCenter.X - entry.Pos.X;
            var dz = rockCenter.Z - entry.Pos.Y;
            if (dx * dx + dz * dz <= radiusSq) return true;
        }
        return false;
    }

    private void TelegraphHalfRoomCleave(int index, bool eastUnsafe)
    {
        var activeProp = eastUnsafe ? statuePropCleaveEast : statuePropCleaveWest;
        activeProp?.PlayBeat(EObjAnimCleaveWindUp.State, EObjAnimCleaveWindUp.Bitmask);
        DiagnosticLog.Info($"[UmadP1GI2] Half-room cleave {index} telegraph: {(eastUnsafe ? "East unsafe (CrushingAura)" : "West unsafe (GravityWave)")}");
    }

    private void ResolveHalfRoomCleave(bool eastUnsafe)
    {
        var activeProp = eastUnsafe ? statuePropCleaveEast : statuePropCleaveWest;
        activeProp?.PlayBeat(EObjAnimCleaveResolve.State, EObjAnimCleaveResolve.Bitmask);
        if (cleaveHelper == null) return;
        var heading = eastUnsafe ? MathF.PI / 2f : -MathF.PI / 2f;
        var action = eastUnsafe ? UmadActions.CrushingAura : UmadActions.GravityWave;
        cleaveHelper.SetPosition(new Placement(Vector3.Zero, heading));
        cleaveHelper.Cast(action);
    }

    private void ResolveTrap2Stacks()
    {
        ResolveOneTrapStack(state.Trap2Support);
        ResolveOneTrapStack(state.Trap2Dps);
    }

    private void ResolveOneTrapStack(PartyRole holderRole)
    {
        if (party.Get(holderRole) is not { } holder || !holder.IsAlive()) return;
        kefka?.CastFromHelper(UmadActions.DoubleTroubleTrapStack, holder);

        SimCharacter? nextHolder = null;
        var bestDistSq = 36f;
        foreach (var member in party.ActiveMembers())
        {
            if (member == holder || !member.IsAlive()) continue;
            var dx = member.Position.X - holder.Position.X;
            var dz = member.Position.Z - holder.Position.Z;
            var dSq = dx * dx + dz * dz;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                nextHolder = member;
            }
        }
        nextHolder?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 49.0f);
    }

    private void ResolveHeavyGravityWave(int wave, IReadOnlyList<Vector2> drops, List<PuddleEntry> wavePuddles)
    {
        var fallbackCount = 0;
        for (var i = 0; i < wavePuddles.Count; i++)
        {
            var entry = wavePuddles[i];
            if (entry.Resolved) continue;
            entry.Resolved = true;
            if (entry.Prop.IsAlive) entry.Prop.PlayBeat(EObjAnimDespawn.State, EObjAnimDespawn.Bitmask);
            kefka?.CastFromHelper(UmadActions.HeavyGravity, at: new Placement(new Vector3(entry.Pos.X, 0f, entry.Pos.Y), 0f));
            fallbackCount++;
        }
        if (fallbackCount > 0)
            DiagnosticLog.Info($"[UmadP1GI2] HeavyGravity wave {wave} fallback resolved {fallbackCount} puddle(s).");
    }
}