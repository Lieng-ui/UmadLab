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
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P1GravenImage1;
using AnoMech.Scenarios.Umad.P1GravenImage2;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;

namespace AnoMech.Scenarios.Umad.P1Full;

using Constants = UmadConstants;

public sealed class UmadP1FullScenario : IScenario
{
    public string Name => "Full";
    public IPhase Phase => UmadZone.P1;
    public float BgmSecondsAtStart => 0f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP1FullAi()];
    public object SettingsOverrides => settingsWindow.Overrides;

    private const float Gi2Offset = 78.0f;
    private const float Gi3Offset = 146.0f;

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
    private UmadP1FullState state = null!;
    private SimEnemy? kefka;

    private readonly List<SimEnemy> gi1Helpers = [];
    private readonly List<Vector3> gi1Towers = [];

    private readonly List<SimEnemy> gi2GravityHelpers = [];
    private readonly List<SimEnemy> gi2RockHelpers = [];
    private SimEnemy? gi2CleaveHelper;
    private readonly List<PuddleEntry> r1Puddles = [];
    private readonly List<PuddleEntry> r2Puddles = [];
    private int puddlesPlaced;
    private bool puddlesClearable;
    private bool gi2Wiped;

    private readonly Dictionary<PartyRole, SimEnemy> gi3TetherStatues = [];
    private readonly Dictionary<PartyRole, Placement> pendingArrowPlacements = [];
    private readonly List<SimEventObject> activeArrows = [];
    private readonly Dictionary<(SimEventObject Arrow, PartyRole Role), float> arrowDwell = [];
    private readonly Dictionary<SimEventObject, float> arrowGrace = [];
    private readonly Dictionary<SimEventObject, float> arrowLifespan = [];
    private readonly Dictionary<PartyRole, float> arrowPushLock = [];
    private int arrowsPlaced;
    private int arrowsSoakedByConfused;
    private readonly List<string> arrowSoakFailures = [];

    private SimEventObject? statuePropA1;
    private SimEventObject? statuePropA2;
    private SimEventObject? statuePropB1;
    private SimEventObject? statuePropC1;
    private SimEventObject? statuePropB2;
    private SimEventObject? statuePropC2;
    private SimEventObject? gazeStatueNormal;
    private SimEventObject? gazeStatueInverted;
    private SimEventObject? gazeAnimStatueNormal;
    private SimEventObject? gazeAnimStatueInverted;
    private SimEventObject? colossusProp;

    private readonly UmadP1FullSettingsWindow settingsWindow = new();

    private static readonly Vector3 UpperStatuePos = new(0f, 18.5f, -44f);
    private static readonly Vector3 LowerStatuePos = new(0f, 0f, -35f);
    private static readonly Vector3 GravityStatuePos = new(2.5f, 22.5f, -73.0f);
    private static readonly Vector3 RockStatuePos = new(26.0f, 7.0f, -58.5f);
    private static readonly Vector3 CleaveWestStatuePos = new(-8.0f, 15.0f, -73.0f);
    private static readonly Vector3 CleaveEastStatuePos = new(16.0f, 6.5f, -57.0f);
    private static readonly Vector3 GazeStatueNormalPos = new(7f, 8.5f, -57f);
    private static readonly Vector3 GazeStatueNormalAnimPos = new(5.25f, 13.5f, -66f);
    private static readonly Vector3 GazeStatueInvertedPos = new(-5f, 27f, -75f);
    private static readonly Vector3 GazeStatueInvertedAnimPos = new(-5f, 12.5f, -75f);

    private static readonly (uint State, uint Bitmask) EObjAnimAppear = (0x0001, 0x0002);
    private static readonly (uint State, uint Bitmask) EObjAnimTetherFire = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimWindUp = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimResolve = (0x0100, 0x0200);
    private static readonly (uint State, uint Bitmask) EObjAnimDespawn = (0x0004, 0x0008);
    private static readonly (uint State, uint Bitmask) EObjAnimStatueCollapse = (0x0004, 0x0200);

    private static readonly float[] IcePairA = [-MathF.PI / 4f, 3f * MathF.PI / 4f];
    private static readonly float[] IcePairB = [-3f * MathF.PI / 4f, MathF.PI / 4f];

    private const float TowerSoakRadius = 4.0f;
    private const float PuddleArmGrace = 1.65f;
    private const float PuddleTouchRadius = 2.2f;
    private const float PuddleClearRadius = 3.2f;
    private const float PuddleClearFuse = 1.25f;
    private const float RockBulletRadius = 6.0f;
    private const float ArrowTriggerRadius = 2.0f;
    private const float ArrowCollideDistance = 4.0f;
    private const float ArrowPushDistance = 6.0f;
    private const float ArrowBindDuration = 1.000f;
    private const float ArrowTriggerDwell = 0.35f;
    private const float ArrowGraceDuration = 3.0f;
    private const float ArrowLifespanAfterSpawn = 18.687f;
    private const float PostCarryWaitDuration = 2.000f;
    private const float RetiredArrowDespawnDelay = 3.26f;
    private const float Gi3ConfusedChaseStart = 29.09f;
    private const float Gi3ConfusedChaseEnd = 35.09f;

    private static readonly EnemyAction GravityPuddleWipe = UmadActions.UnmitigatedImpact with
    {
        DeathExplanation = "stepped into a gravity puddle before it became clearable",
    };

    private static readonly EnemyAction RockBulletWipe = UmadActions.UnmitigatedImpact with
    {
        DeathExplanation = "Rock Bullet hit a gravity puddle or another player",
    };

    public UmadP1FullState? LastState { get; private set; }

    public void DrawSettings() => settingsWindow.Draw();

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
    }

    private void At(float fightTime, Action action)
        => world.Events.Add(state.CountdownSeconds + fightTime, action);

    private void AtGi2(float gi2Time, Action action)
        => At(Gi2Offset + gi2Time, action);

    private void AtGi3(float gi3Time, Action action)
        => At(Gi3Offset + gi3Time, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UmadP1FullState(world.Rng, settingsWindow.Overrides);
        LastState = state;

        CleanupObjects();

        world.Events.Add(0f, SpawnInitialActors);
        ScheduleCountdownLogs();

        ScheduleGravenImage1();
        ScheduleGravenImage2();
        ScheduleGravenImage3();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP1FullState>)AiStrats[idx]).Run(state, world);
    }

    public void Tick(float delta, float elapsed)
    {
        if (!gi2Wiped)
        {
            if (!puddlesClearable)
            {
                CheckPuddleTouch(r1Puddles, elapsed);
                if (!gi2Wiped) CheckPuddleTouch(r2Puddles, elapsed);
            }
            else
            {
                TickClearablePuddles(r2Puddles, elapsed);
                TickClearablePuddles(r1Puddles, elapsed);
            }
        }
        TickArrows(delta, elapsed);
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
                    Plugin.ChatGui?.Print($"[绝妖星 P1] 战斗开始倒计时：{captured}秒");
                    DiagnosticLog.Info($"[UmadP1Full] Pull countdown: {captured}s");
                });
            }
        }
        world.Events.Add(state.CountdownSeconds, () =>
        {
            Plugin.ChatGui?.Print("[绝妖星 P1] 战斗开始！");
            DiagnosticLog.Info("[UmadP1Full] Pull countdown: ENGAGE!");
        });
    }

    private void CleanupObjects()
    {
        foreach (var h in gi1Helpers) h.Despawn();
        gi1Helpers.Clear();
        gi1Towers.Clear();

        foreach (var h in gi2GravityHelpers) h.Despawn();
        gi2GravityHelpers.Clear();
        foreach (var h in gi2RockHelpers) h.Despawn();
        gi2RockHelpers.Clear();
        gi2CleaveHelper?.Despawn();
        gi2CleaveHelper = null;
        DespawnPuddles(r1Puddles);
        DespawnPuddles(r2Puddles);
        puddlesPlaced = 0;
        puddlesClearable = false;
        gi2Wiped = false;
        state.Gi2.ClearGravityDrops();

        foreach (var staleArrow in activeArrows.ToArray()) RemoveArrow(staleArrow);
        arrowPushLock.Clear();
        arrowDwell.Clear();
        pendingArrowPlacements.Clear();
        arrowsPlaced = 0;
        arrowsSoakedByConfused = 0;
        arrowSoakFailures.Clear();
        foreach (var s in gi3TetherStatues.Values) s.Despawn();
        gi3TetherStatues.Clear();

        DespawnProp(ref statuePropA1);
        DespawnProp(ref statuePropA2);
        DespawnProp(ref statuePropB1);
        DespawnProp(ref statuePropC1);
        DespawnProp(ref statuePropB2);
        DespawnProp(ref statuePropC2);
        DespawnProp(ref gazeStatueNormal);
        DespawnProp(ref gazeStatueInverted);
        DespawnProp(ref gazeAnimStatueNormal);
        DespawnProp(ref gazeAnimStatueInverted);
        DespawnProp(ref colossusProp);
        world.Obstacles.Clear();
    }

    private void DespawnProp(ref SimEventObject? prop)
    {
        if (prop == null) return;
        if (prop.IsAlive) prop.PlayBeat(EObjAnimDespawn.State, EObjAnimDespawn.Bitmask);
        prop.Despawn();
        prop = null;
    }

    private static void DespawnPuddles(List<PuddleEntry> puddles)
    {
        foreach (var entry in puddles) entry.Prop.Despawn();
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
        gazeStatueNormal = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.GazeStatueNormal,
            LayoutId = Constants.EObjId.GazeStatueNormalLayoutId,
            EventId = propEventId,
            EntityId = 0x4000EB80u,
            TargetableStatus = 5,
            Arg2 = 0x00490003u,
            Placement = new Placement(GazeStatueNormalPos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        gazeStatueInverted = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.GazeStatueInverted,
            LayoutId = Constants.EObjId.GazeStatueInvertedLayoutId,
            EventId = propEventId,
            EntityId = 0x4000EB81u,
            TargetableStatus = 5,
            Arg2 = 0x004A0003u,
            Placement = new Placement(GazeStatueInvertedPos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        gazeAnimStatueNormal = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.GazeStatueNormal,
            LayoutId = Constants.EObjId.GazeStatueNormalAnimLayoutId,
            EventId = propEventId,
            EntityId = 0x4000EB82u,
            TargetableStatus = 5,
            Arg2 = 0x00480003u,
            Placement = new Placement(GazeStatueNormalAnimPos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        gazeAnimStatueInverted = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.GazeStatueInverted,
            LayoutId = Constants.EObjId.GazeStatueInvertedAnimLayoutId,
            EventId = propEventId,
            EntityId = 0x4000EB83u,
            TargetableStatus = 5,
            Arg2 = 0x00470003u,
            Placement = new Placement(GazeStatueInvertedAnimPos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropA1 = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueA,
            LayoutId = Constants.EObjId.EarlierStatueA1LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase,
            TargetableStatus = 5,
            Arg2 = 0x00420003U,
            Placement = new Placement(UpperStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropA2 = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueA,
            LayoutId = Constants.EObjId.EarlierStatueA2LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 1u,
            TargetableStatus = 5,
            Arg2 = 0x00410003U,
            Placement = new Placement(LowerStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropB1 = world.SpawnEventObject(new EventObjectSpawnConfig
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
        statuePropC1 = world.SpawnEventObject(new EventObjectSpawnConfig
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
        statuePropB2 = world.SpawnEventObject(new EventObjectSpawnConfig
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
        statuePropC2 = world.SpawnEventObject(new EventObjectSpawnConfig
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
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });

        for (var i = 0; i < 4; i++)
        {
            if (SpawnGravenHelper(UpperStatuePos) is { } h1) gi1Helpers.Add(h1);
            if (SpawnGravenHelper(GravityStatuePos) is { } hg) gi2GravityHelpers.Add(hg);
            if (SpawnGravenHelper(RockStatuePos) is { } hr) gi2RockHelpers.Add(hr);
        }
        gi2CleaveHelper = SpawnGravenHelper(Vector3.Zero);

        foreach (var role in state.Gi3.Debuffs.Keys)
        {
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.Gi3.DpsGetsConfused;
            var pos = confused ? UmadP1TeleTrouncingScenario.ConfusedStatuePos : UmadP1TeleTrouncingScenario.SleepStatuePos;
            if (SpawnGravenHelper(pos) is { } ts) gi3TetherStatues[role] = ts;
        }
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

    // ===================== PHASE 1A: GRAVEN IMAGE 1 (0 - 78s) =====================

    private void ScheduleGravenImage1()
    {
        var s = state.Gi1;
        foreach (var at in new[] { 4.48f, 7.50f, 22.80f, 61.80f, 74.20f })
            At(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank), animationVariation: 1));

        At(11.60f, () =>
        {
            if (party.Get(PartyRole.MainTank) is not { } mt) return;
            mt.AttachLockonVfx(Constants.LockonId.TankBuster);
            kefka?.Cast(UmadActions.ViciousDestruction1, mt);
        });
        At(19.80f, () =>
        {
            if (party.Get(PartyRole.MainTank) is { } mt) kefka?.Cast(UmadActions.ViciousDestruction2, mt);
        });
        At(26.00f, () => kefka?.Cast(Constants.ActionId.KefkaPoof, animationLock: 1.1f));

        At(27.42f, () =>
        {
            kefka?.Cast(Constants.ActionId.GravenImage, animationLock: 2.1f);
            colossusProp?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            statuePropA1?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            statuePropA2?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
        });
        At(31.17f, Gi1TetherWaveBullet);

        At(33.49f, () =>
        {
            kefka?.AttachLockonVfx(s.Ice1IsLie ? Constants.LockonId.ColdFalse : Constants.LockonId.ColdTrue);
            kefka?.AttachLockonVfx(s.Fire1IsLie ? Constants.LockonId.FireFalse : Constants.LockonId.FireTrue);
            Gi1AttachFire1Markers();
        });
        At(33.57f, () =>
        {
            kefka?.Cast(Constants.ActionId.MysteryMagic, animationLock: 3.1f);
            SpawnIceCones(s.Ice1DiagonalB, s.Ice1IsLie);
        });
        At(36.25f, Gi1ResolveWaveBulletKnockback);
        At(39.37f, Gi1ResolveFire1);

        At(42.75f, () => kefka?.Cast(Constants.ActionId.DoubleTroubleTrapCast, animationLock: 2.1f));
        At(43.55f, () => statuePropA2?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask));
        At(43.60f, Gi1ResolveWaveCannonLasers);
        At(44.22f, Gi1SpawnExplosionTowers);
        At(45.73f, () =>
        {
            party.Get(s.Trap1Support)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 5.0f);
            party.Get(s.Trap1Dps)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 5.0f);
        });
        At(47.20f, Gi1JudgeExplosionTowers);

        At(49.74f, () =>
        {
            kefka?.AttachLockonVfx(s.Ice2IsLie ? Constants.LockonId.ColdFalse : Constants.LockonId.ColdTrue);
            kefka?.AttachLockonVfx(s.ThunderIsLie ? Constants.LockonId.LightningFalse : Constants.LockonId.LightningTrue);
        });
        At(49.83f, () =>
        {
            kefka?.Cast(Constants.ActionId.MysteryMagic, animationLock: 3.1f);
            SpawnIceCones(s.Ice2DiagonalB, s.Ice2IsLie);
            SpawnThunderLines(s.ThunderRealOffset, s.ThunderOrientation, s.ThunderIsLie);
        });
        At(50.85f, () =>
        {
            ResolveTrapStack(s.Trap1Support, state.Gi2.Trap2Support, 68.6f);
            ResolveTrapStack(s.Trap1Dps, state.Gi2.Trap2Dps, 68.6f);
        });

        At(58.91f, () => kefka?.Cast(UmadActions.LightOfJudgmentP1));
        foreach (var at in new[] { 67.06f, 69.15f, 71.24f })
            At(at, () =>
            {
                var target = party.Get(PartyRole.OffTank) ?? party.Get(PartyRole.MainTank);
                if (target != null) kefka?.Cast(UmadActions.Overdrive, target);
            });
    }

    private void Gi1TetherWaveBullet()
    {
        var targets = state.Gi1.DpsGetsTether ? UmadP1GravenImage1State.DpsRoles : UmadP1GravenImage1State.SupportRoles;
        for (var i = 0; i < targets.Length && i < gi1Helpers.Count; i++)
            if (party.Get(targets[i]) is { } member)
                world.Tether(gi1Helpers[i], member, Constants.TetherId.GravenImage, duration: 5.08f);
    }

    private void Gi1ResolveWaveBulletKnockback()
    {
        statuePropA1?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        var targets = state.Gi1.DpsGetsTether ? UmadP1GravenImage1State.DpsRoles : UmadP1GravenImage1State.SupportRoles;
        for (var i = 0; i < targets.Length && i < gi1Helpers.Count; i++)
        {
            if (party.Get(targets[i]) is not { } member || !member.IsAlive()) continue;
            gi1Helpers[i].Cast(UmadActions.WaveBullet, member);
            var away = new Vector2(member.Position.X - UpperStatuePos.X, member.Position.Z - UpperStatuePos.Z);
            away = away.LengthSquared() < 0.01f ? new Vector2(0f, 1f) : Vector2.Normalize(away);
            var dest2 = new Vector2(member.Position.X, member.Position.Z) + away * 10.0f;
            if (dest2.Length() > 18.5f) dest2 = Vector2.Normalize(dest2) * 18.5f;
            var dest = new Vector3(dest2.X, member.Position.Y, dest2.Y);
            var target = member;
            world.Events.Add(0.60f, () => { if (target.IsAlive()) target.CarryTo(dest); });
        }
    }

    private void Gi1AttachFire1Markers()
    {
        var s = state.Gi1;
        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            if (party.Get(role) is not { } member) continue;
            if (s.Fire1ShowsStackIcon)
            {
                if (role == s.Fire1StackSupport || role == s.Fire1StackDps)
                    member.AttachLockonVfx(Constants.LockonId.FireStack);
            }
            else
            {
                member.AttachLockonVfx(Constants.LockonId.FireSpread);
            }
        }
    }

    private void Gi1ResolveFire1()
    {
        var s = state.Gi1;
        if (s.Fire1IsStack)
        {
            if (party.Get(s.Fire1StackSupport) is { } sup && sup.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireStack, sup);
            if (party.Get(s.Fire1StackDps) is { } dps && dps.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireStack, dps);
            return;
        }
        foreach (var role in UmadP1GravenImage1State.AllRoles)
            if (party.Get(role) is { } member && member.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireSpread, member);
    }

    private void Gi1ResolveWaveCannonLasers()
    {
        gi1Towers.Clear();
        for (var i = 0; i < state.Gi1.LaserTargets.Count && i < gi1Helpers.Count; i++)
        {
            var role = state.Gi1.LaserTargets[i];
            if (party.Get(role) is not { } target || !target.IsAlive()) continue;
            var heading = MathF.Atan2(target.Position.X - LowerStatuePos.X, target.Position.Z - LowerStatuePos.Z);
            gi1Helpers[i].SetPosition(new Placement(LowerStatuePos, heading));
            gi1Helpers[i].Cast(UmadActions.WaveCannonLaser, target);
            gi1Towers.Add(new Vector3(target.Position.X, 0f, target.Position.Z));
            state.Gi1.RecordTowerSpot(role, new Vector2(target.Position.X, target.Position.Z));
        }
    }

    private void Gi1SpawnExplosionTowers()
    {
        foreach (var spot in gi1Towers)
            kefka?.CastFromHelper(UmadActions.TowerExplosion, at: new Placement(spot, 0f));
    }

    private void Gi1JudgeExplosionTowers()
    {
        var missed = 0;
        for (var i = 0; i < gi1Towers.Count; i++)
        {
            var spot = gi1Towers[i];
            var soakers = 0;
            foreach (var member in party.ActiveMembers())
            {
                if (!member.IsAlive()) continue;
                var dx = member.Position.X - spot.X;
                var dz = member.Position.Z - spot.Z;
                if (dx * dx + dz * dz <= TowerSoakRadius * TowerSoakRadius) soakers++;
            }
            if (soakers == 0) missed++;
        }
        if (missed == 0) return;
        kefka?.Cast(UmadActions.TowerBigBang);
        world.Fail($"Graven Image 1 tower unsoaked ({missed}/{gi1Towers.Count} empty)");
    }

    // ===================== PHASE 1B: GRAVEN IMAGE 2 (78 - 147s) =====================

    private void ScheduleGravenImage2()
    {
        var s = state.Gi2;
        foreach (var at in new[] { 2.50f, 5.50f, 26.50f, 36.00f, 49.00f, 65.50f })
            AtGi2(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank), animationVariation: 1));

        AtGi2(0.45f, () =>
        {
            kefka?.Cast(Constants.ActionId.GravenImage, animationLock: 2.1f);
            colossusProp?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
            statuePropB1?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            statuePropC1?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
        });

        AtGi2(4.19f, () => Gi2TetherGravityAndRock(1, s.R1DpsGetsGravity));
        AtGi2(5.48f, () => kefka?.AttachLockonVfx(s.IceIsLie ? Constants.LockonId.ColdFalse : Constants.LockonId.ColdTrue));
        AtGi2(5.57f, () =>
        {
            kefka?.Cast(Constants.ActionId.BlizzardIIIBlowoutCast, animationLock: 3.1f);
            SpawnIceCones(s.IceDiagonalB, s.IceIsLie);
        });
        AtGi2(10.69f, () => Gi2ResolveGravityBullets(1, s.R1DpsGetsGravity));
        AtGi2(11.29f, () => Gi2SpawnPuddles(1, s.R1GravityDrops, Gi2Offset + 11.29f));
        AtGi2(14.71f, () => Gi2ResolveRockBullets(s.R1DpsGetsGravity));

        AtGi2(15.47f, () =>
        {
            if (party.Get(PartyRole.OffTank) is not { } st) return;
            st.AttachLockonVfx(Constants.LockonId.TankBuster);
            kefka?.Cast(UmadActions.ViciousDestruction1, st);
        });
        AtGi2(23.71f, () =>
        {
            if (party.Get(PartyRole.OffTank) is { } st) kefka?.Cast(UmadActions.ViciousDestruction2, st);
        });

        AtGi2(20.64f, () => Gi2TetherGravityAndRock(2, s.R2DpsGetsGravity));
        AtGi2(21.00f, () => Gi2TelegraphHalfRoomCleave(s.Cleave1EastUnsafe));
        AtGi2(24.51f, () => Gi2ResolveHalfRoomCleave(s.Cleave1EastUnsafe));
        AtGi2(29.14f, () => Gi2ResolveGravityBullets(2, s.R2DpsGetsGravity));
        AtGi2(29.69f, () => Gi2SpawnPuddles(2, s.R2GravityDrops, Gi2Offset + 29.69f));
        AtGi2(33.15f, () => Gi2ResolveRockBullets(s.R2DpsGetsGravity));
        AtGi2(34.20f, () => Gi2TelegraphHalfRoomCleave(s.Cleave2EastUnsafe));
        AtGi2(37.70f, () => Gi2ResolveHalfRoomCleave(s.Cleave2EastUnsafe));

        AtGi2(41.10f, Gi2ActivatePuddles);
        AtGi2(41.49f, () =>
        {
            ResolveTrapStack(s.Trap2Support, state.Gi3.ConfettiStackSupport, 50.2f);
            ResolveTrapStack(s.Trap2Dps, state.Gi3.ConfettiStackDps, 50.2f);
        });
        AtGi2(45.80f, () => Gi2ResolveHeavyGravityWave(s.R2GravityDrops, r2Puddles));
        AtGi2(47.10f, () => DespawnPuddles(r2Puddles));
        AtGi2(49.20f, () => Gi2ResolveHeavyGravityWave(s.R1GravityDrops, r1Puddles));
        AtGi2(50.20f, () => DespawnPuddles(r1Puddles));

        AtGi2(50.67f, () => kefka?.Cast(UmadActions.LightOfJudgmentP1));
        foreach (var at in new[] { 58.82f, 60.91f, 63.01f })
            AtGi2(at, () =>
            {
                if (party.Get(PartyRole.MainTank) is { } mt) kefka?.Cast(UmadActions.Overdrive, mt);
            });
    }

    private void Gi2TetherGravityAndRock(int round, bool dpsGetsGravity)
    {
        var gravTargets = dpsGetsGravity ? UmadP1GravenImage2State.DpsRoles : UmadP1GravenImage2State.SupportRoles;
        var rockTargets = dpsGetsGravity ? UmadP1GravenImage2State.SupportRoles : UmadP1GravenImage2State.DpsRoles;
        var gravDur = round == 1 ? 6.50f : 8.50f;
        var rockDur = round == 1 ? 10.50f : 12.50f;
        for (var i = 0; i < 4; i++)
        {
            if (i < gi2GravityHelpers.Count && party.Get(gravTargets[i]) is { } gm)
                world.Tether(gi2GravityHelpers[i], gm, Constants.TetherId.GravenImage, duration: gravDur);
            if (i < gi2RockHelpers.Count && party.Get(rockTargets[i]) is { } rm)
                world.Tether(gi2RockHelpers[i], rm, Constants.TetherId.GravenImage, duration: rockDur);
        }
    }

    private void Gi2ResolveGravityBullets(int round, bool dpsGetsGravity)
    {
        statuePropB1?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        var targets = dpsGetsGravity ? UmadP1GravenImage2State.DpsRoles : UmadP1GravenImage2State.SupportRoles;
        for (var i = 0; i < targets.Length && i < gi2GravityHelpers.Count; i++)
        {
            if (party.Get(targets[i]) is not { } member || !member.IsAlive()) continue;
            gi2GravityHelpers[i].Cast(UmadActions.GravityBullet, member);
            var drop = new Vector2(member.Position.X, member.Position.Z);
            if (round == 1) state.Gi2.RecordR1GravityDrop(drop);
            else state.Gi2.RecordR2GravityDrop(drop);
        }
    }

    private void Gi2SpawnPuddles(int round, IReadOnlyList<Vector2> drops, float spawnFightTime)
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
    }

    private void Gi2ActivatePuddles()
    {
        puddlesClearable = true;
        foreach (var e in r1Puddles) if (e.Prop.IsAlive) e.Prop.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        foreach (var e in r2Puddles) if (e.Prop.IsAlive) e.Prop.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
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
                    Gi2TriggerWipe(GravityPuddleWipe, new Vector3(entry.Pos.X, 0f, entry.Pos.Y), $"{role} stepped into a gravity puddle early");
                    return;
                }
            }
        }
    }

    private void Gi2ResolveRockBullets(bool dpsGetsGravity)
    {
        statuePropC1?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        var targets = dpsGetsGravity ? UmadP1GravenImage2State.SupportRoles : UmadP1GravenImage2State.DpsRoles;
        const float rockRadiusSq = RockBulletRadius * RockBulletRadius;
        for (var i = 0; i < targets.Length && i < gi2RockHelpers.Count; i++)
        {
            if (party.Get(targets[i]) is not { } member || !member.IsAlive()) continue;
            gi2RockHelpers[i].Cast(UmadActions.RockBullet, member);
            if (gi2Wiped) continue;

            foreach (var otherRole in UmadP1GravenImage2State.AllRoles)
            {
                if (otherRole == targets[i]) continue;
                if (party.Get(otherRole) is not { } other || !other.IsAlive()) continue;
                var dx = other.Position.X - member.Position.X;
                var dz = other.Position.Z - member.Position.Z;
                if (dx * dx + dz * dz <= rockRadiusSq)
                {
                    Gi2TriggerWipe(RockBulletWipe, member.Position, $"{targets[i]} Rock Bullet clipped {otherRole}");
                    break;
                }
            }
            if (gi2Wiped) continue;
            if (RockHitsPuddle(member.Position, r1Puddles, rockRadiusSq)
                || RockHitsPuddle(member.Position, r2Puddles, rockRadiusSq))
            {
                Gi2TriggerWipe(RockBulletWipe, member.Position, $"{targets[i]} Rock Bullet hit a gravity puddle");
            }
        }
    }

    private static bool RockHitsPuddle(Vector3 center, List<PuddleEntry> puddles, float radiusSq)
    {
        for (var i = 0; i < puddles.Count; i++)
        {
            var e = puddles[i];
            if (!e.Prop.IsAlive) continue;
            var dx = center.X - e.Pos.X;
            var dz = center.Z - e.Pos.Y;
            if (dx * dx + dz * dz <= radiusSq) return true;
        }
        return false;
    }

    private void Gi2TriggerWipe(EnemyAction wipeAction, Vector3 origin, string reason)
    {
        if (gi2Wiped) return;
        gi2Wiped = true;
        DiagnosticLog.Warn($"[UmadP1Full] GI2 wipe: {reason}");
        kefka?.CastFromHelper(wipeAction, at: new Placement(origin, 0f));
        world.Fail(reason);
    }

    private void Gi2TelegraphHalfRoomCleave(bool eastUnsafe)
        => (eastUnsafe ? statuePropC2 : statuePropB2)?.PlayBeat(EObjAnimWindUp.State, EObjAnimWindUp.Bitmask);

    private void Gi2ResolveHalfRoomCleave(bool eastUnsafe)
    {
        (eastUnsafe ? statuePropC2 : statuePropB2)?.PlayBeat(EObjAnimResolve.State, EObjAnimResolve.Bitmask);
        if (gi2CleaveHelper == null) return;
        var heading = eastUnsafe ? MathF.PI / 2f : -MathF.PI / 2f;
        gi2CleaveHelper.SetPosition(new Placement(Vector3.Zero, heading));
        gi2CleaveHelper.Cast(eastUnsafe ? UmadActions.CrushingAura : UmadActions.GravityWave);
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

    private void Gi2ResolveHeavyGravityWave(IReadOnlyList<Vector2> drops, List<PuddleEntry> wavePuddles)
    {
        for (var i = 0; i < wavePuddles.Count; i++)
        {
            var e = wavePuddles[i];
            if (e.Resolved) continue;
            e.Resolved = true;
            if (e.Prop.IsAlive) e.Prop.PlayBeat(EObjAnimDespawn.State, EObjAnimDespawn.Bitmask);
            kefka?.CastFromHelper(UmadActions.HeavyGravity, at: new Placement(new Vector3(e.Pos.X, 0f, e.Pos.Y), 0f));
        }
    }

    // ===================== PHASE 1C: GRAVEN IMAGE 3 / TELE-TROUNCING (147 - 205s) =====================

    private void ScheduleGravenImage3()
    {
        var s = state.Gi3;
        AtGi3(1.59f, () => kefka?.Cast(Constants.ActionId.TeleTrouncing, animationLock: 3.1f));
        foreach (var at in new[] { 7.45f, 10.47f, 13.48f, 34.48f, 45.75f, 48.77f })
            AtGi3(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank), animationVariation: 1));

        AtGi3(7.34f, Gi3ApplyArrowDebuffs);
        AtGi3(14.42f, () => Gi3SpawnArrowBursts(first: true));
        AtGi3(15.29f, Gi3SpawnArrowObjects);
        AtGi3(15.67f, () =>
        {
            kefka?.Cast(Constants.ActionId.GravenImage, animationLock: 2.1f);
            colossusProp?.PlayBeat(EObjAnimWindUp.State, EObjAnimWindUp.Bitmask);
        });
        AtGi3(17.43f, () => Gi3SpawnArrowBursts(first: false));
        AtGi3(18.32f, Gi3SpawnArrowObjects);

        AtGi3(19.41f, Gi3TetherStatues);
        AtGi3(19.43f, () =>
        {
            gazeStatueNormal?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            gazeStatueInverted?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
        });
        AtGi3(22.78f, () =>
        {
            ResolveTrapStack(s.ConfettiStackSupport, null, 0f);
            ResolveTrapStack(s.ConfettiStackDps, null, 0f);
        });

        AtGi3(28.40f, () =>
        {
            gazeStatueNormal?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
            gazeStatueInverted?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        });
        AtGi3(28.46f, Gi3ResolveGravenWills);
        AtGi3(Gi3ConfusedChaseStart, () =>
        {
            world.Obstacles.Clear();
            Gi3ApplyConfusedAndSleepStatuses();
        });
        AtGi3(31.61f, () => Gi3GazingProp?.PlayBeat(EObjAnimWindUp.State, EObjAnimWindUp.Bitmask));
        AtGi3(Gi3ConfusedChaseEnd, () =>
        {
            Gi3ReleaseConfusedControl();
            Gi3JudgeArrowSoaks();
        });

        AtGi3(36.31f, () =>
        {
            kefka?.AttachLockonVfx(s.ThunderIsLie ? Constants.LockonId.LightningFalse : Constants.LockonId.LightningTrue);
            kefka?.AttachLockonVfx(s.FireIsLie ? Constants.LockonId.FireFalse : Constants.LockonId.FireTrue);
            Gi3AttachFireMarkers();
        });
        AtGi3(36.41f, () =>
        {
            kefka?.Cast(Constants.ActionId.MysteryMagic, animationLock: 3.1f);
            SpawnThunderLines(s.ThunderRealOffset, s.ThunderOrientation, s.ThunderIsLie);
        });
        AtGi3(41.49f, Gi3ResolveGaze);
        AtGi3(42.19f, Gi3ResolveFire);

        AtGi3(52.68f, () =>
        {
            if (!ArrowSoakFailed) kefka?.SetTargetable(false);
            world.Obstacles.Clear();
        });
        AtGi3(55.96f, () => colossusProp?.PlayBeat(EObjAnimStatueCollapse.State, EObjAnimStatueCollapse.Bitmask));
        AtGi3(62.03f, () => { });
    }

    private SimEnemy? Gi3GazingStatue => gi3TetherStatues
        .LastOrDefault(kv => (UmadP1TeleTrouncingState.IsDps(kv.Key) == state.Gi3.DpsGetsConfused) == state.Gi3.GazeInverted).Value;

    private SimEventObject? Gi3GazingProp => state.Gi3.GazeInverted ? gazeAnimStatueInverted : gazeAnimStatueNormal;

    private bool ArrowSoakFailed => arrowSoakFailures.Count > 0 || arrowsSoakedByConfused < arrowsPlaced;

    private void Gi3ApplyArrowDebuffs()
    {
        foreach (var (role, (first, second)) in state.Gi3.Debuffs)
        {
            var member = party.Get(role);
            var firstSource = kefka?.GameObjectId ?? default;
            if (first == second)
            {
                member?.AddStatus(PrimaryArrowStatus(first), 7.000f, sourceObject: firstSource);
                member?.AddStatus(PrimaryArrowStatus(second), 10.000f);
            }
            else
            {
                var firstAt7 = !state.Gi3.DifferentPolarity[role];
                member?.AddStatus(PrimaryArrowStatus(first), firstAt7 ? 7.000f : 10.000f);
                member?.AddStatus(SecondaryArrowStatus(second), firstAt7 ? 10.000f : 7.000f);
            }
        }
    }

    private void Gi3SpawnArrowBursts(bool first)
    {
        foreach (var role in state.Gi3.Debuffs.Keys)
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            var (at7, at10) = state.Gi3.ExpiryDirections(role);
            var dir = first ? at7 : at10;
            pendingArrowPlacements[role] = new Placement(new Vector3(member.Position.X, 0f, member.Position.Z), ArrowHeading(dir));
            kefka?.CastFromHelper(UmadActions.TeleTrouncingArrowSpawn, member);
        }
    }

    private void Gi3SpawnArrowObjects()
    {
        var propEventId = settingsWindow.Overrides.PropsBindDirector ? Constants.EObjId.PropEventId : 0u;
        foreach (var placement in pendingArrowPlacements.Values)
        {
            var slot = 8 + arrowsPlaced;
            var arrow = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.TelePortent,
                Placement = placement,
                TargetableStatus = 5,
                EventId = propEventId,
                EntityId = Constants.Spawn.TelePortentEntityIdBase + (uint)slot,
                Arg2 = ((uint)(Constants.Spawn.TelePortentArg2Serial + slot) << 16) | 0x3U,
            });
            if (arrow == null) continue;
            activeArrows.Add(arrow);
            arrowsPlaced++;
            arrowGrace[arrow] = ArrowGraceDuration;
            arrowLifespan[arrow] = ArrowLifespanAfterSpawn;
            world.Obstacles.Add(new CircleObstacle(new Vector2(placement.Position.X, placement.Position.Z), ArrowTriggerRadius));
        }
        pendingArrowPlacements.Clear();
    }

    private void Gi3TetherStatues()
    {
        foreach (var (role, statue) in gi3TetherStatues)
            if (party.Get(role) is { } member)
                world.Tether(statue, member, Constants.TetherId.GravenImage, duration: 9.05f);
    }

    private void Gi3ResolveGravenWills()
    {
        foreach (var (role, statue) in gi3TetherStatues)
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.Gi3.DpsGetsConfused;
            statue.Cast(confused ? UmadActions.IndulgentWill : UmadActions.IdyllicWill, member);
        }
    }

    private void Gi3ApplyConfusedAndSleepStatuses()
    {
        foreach (var role in state.Gi3.Debuffs.Keys)
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.Gi3.DpsGetsConfused;
            if (confused) member.ActorControl.SetConfused(true);
            else member.AddStatus(Constants.StatusId.Sleep, 6.000f);
        }
    }

    private void Gi3ReleaseConfusedControl()
    {
        foreach (var role in state.Gi3.Debuffs.Keys)
        {
            if (UmadP1TeleTrouncingState.IsDps(role) != state.Gi3.DpsGetsConfused) continue;
            if (party.Get(role) is { } member) member.ActorControl.SetConfused(false);
        }
    }

    private void TickArrows(float delta, float elapsed)
    {
        foreach (var role in arrowPushLock.Keys.ToArray())
        {
            var rem = arrowPushLock[role] - delta;
            if (rem > 0f) arrowPushLock[role] = rem;
            else arrowPushLock.Remove(role);
        }
        if (activeArrows.Count == 0) return;

        var snapshot = activeArrows.ToArray();
        for (var i = 0; i < snapshot.Length; i++)
        {
            var a = snapshot[i];
            if (!activeArrows.Contains(a)) continue;
            for (var j = i + 1; j < snapshot.Length; j++)
            {
                var b = snapshot[j];
                if (!activeArrows.Contains(b)) continue;
                var adx = a.Position.X - b.Position.X;
                var adz = a.Position.Z - b.Position.Z;
                if (adx * adx + adz * adz > ArrowCollideDistance * ArrowCollideDistance) continue;
                NoteArrowSoakFailure($"two arrows collided at t={elapsed:F2}s");
                RemoveArrow(a);
                RemoveArrow(b);
                break;
            }
        }
        if (activeArrows.Count == 0) return;

        foreach (var arrow in activeArrows.ToArray())
        {
            var lifespan = arrowLifespan.GetValueOrDefault(arrow) - delta;
            if (lifespan <= 0f)
            {
                NoteArrowSoakFailure($"arrow expired unused at t={elapsed:F2}s");
                RetireArrow(arrow);
                continue;
            }
            arrowLifespan[arrow] = lifespan;

            var grace = arrowGrace.GetValueOrDefault(arrow) - delta;
            if (grace > 0f) { arrowGrace[arrow] = grace; continue; }
            arrowGrace.Remove(arrow);

            foreach (var role in state.Gi3.Debuffs.Keys)
            {
                if (party.Get(role) is not { } member) continue;
                var dx = member.Position.X - arrow.Position.X;
                var dz = member.Position.Z - arrow.Position.Z;
                if (dx * dx + dz * dz > ArrowTriggerRadius * ArrowTriggerRadius)
                {
                    arrowDwell.Remove((arrow, role));
                    continue;
                }
                var dwell = arrowDwell.GetValueOrDefault((arrow, role)) + delta;
                arrowDwell[(arrow, role)] = dwell;
                if (dwell < ArrowTriggerDwell || arrowPushLock.GetValueOrDefault(role) > 0f) continue;

                if (member.ActorControl.Confused) arrowsSoakedByConfused++;
                else NoteArrowSoakFailure($"{role} triggered an arrow while not Confused at t={elapsed:F2}s");
                UseArrow(role, member, arrow);
                RetireArrow(arrow);
                break;
            }
        }
    }

    private void UseArrow(PartyRole role, SimCharacter member, SimEventObject arrow)
    {
        member.HoldConfusedChase(ArrowBindDuration + PostCarryWaitDuration);
        member.AddStatus(Constants.StatusId.Bind, ArrowBindDuration);
        arrowPushLock[role] = ArrowBindDuration;
        var h = arrow.Rotation;
        var dest = arrow.Position + new Vector3(MathF.Sin(h), 0f, MathF.Cos(h)) * ArrowPushDistance;
        member.CarryTo(dest);
    }

    private void UntrackArrow(SimEventObject arrow)
    {
        activeArrows.Remove(arrow);
        arrowGrace.Remove(arrow);
        arrowLifespan.Remove(arrow);
        foreach (var key in arrowDwell.Keys.Where(k => k.Arrow == arrow).ToArray()) arrowDwell.Remove(key);
    }

    private void RemoveArrow(SimEventObject arrow)
    {
        UntrackArrow(arrow);
        arrow.Despawn();
    }

    private void RetireArrow(SimEventObject arrow)
    {
        UntrackArrow(arrow);
        arrow.DirectorEObjMod(Constants.EObjState.TelePortentUsed);
        world.Events.Add(RetiredArrowDespawnDelay, arrow.Despawn);
    }

    private void NoteArrowSoakFailure(string what)
    {
        arrowSoakFailures.Add(what);
        DiagnosticLog.Warn($"[UmadP1Full] Arrow soak failure: {what}");
        world.Fail($"Arrow soak failed: {what}");
    }

    private void Gi3JudgeArrowSoaks()
    {
        if (!ArrowSoakFailed) return;
        if (arrowSoakFailures.Count == 0)
            world.Fail($"Arrow soak failed: only {arrowsSoakedByConfused}/{arrowsPlaced} soaked");
    }

    private void Gi3AttachFireMarkers()
    {
        var s = state.Gi3;
        foreach (var role in s.Debuffs.Keys)
        {
            if (party.Get(role) is not { } member) continue;
            if (s.FireShowsStackIcon)
            {
                if (role == s.FireStackSupport || role == s.FireStackDps)
                    member.AttachLockonVfx(Constants.LockonId.FireStack);
            }
            else
            {
                member.AttachLockonVfx(Constants.LockonId.FireSpread);
            }
        }
    }

    private void Gi3ResolveGaze()
    {
        Gi3GazingProp?.PlayBeat(EObjAnimResolve.State, EObjAnimResolve.Bitmask);
        if (Gi3GazingStatue is not { } statue) return;
        statue.SetPosition(new Placement(state.Gi3.GazeInverted ? GazeStatueInvertedAnimPos : GazeStatueNormalAnimPos, 0f));
        statue.Cast(state.Gi3.GazeInverted ? UmadActions.AveMaria : UmadActions.IndolentWill);
    }

    private void Gi3ResolveFire()
    {
        var s = state.Gi3;
        if (s.FireIsStack)
        {
            if (party.Get(s.FireStackSupport) is { } sup && sup.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireStack, sup);
            if (party.Get(s.FireStackDps) is { } dps && dps.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireStack, dps);
            return;
        }
        foreach (var role in s.Debuffs.Keys)
            if (party.Get(role) is { } member && member.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireSpread, member);
    }

    // ===================== SHARED HELPERS =====================

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

    private void SpawnThunderLines(int realOffset, float orientation, bool isLie)
    {
        var realLine = isLie ? UmadActions.ThrummingThunderReal2 : UmadActions.ThrummingThunderReal1;
        var anchors = UmadP1TeleTrouncingScenario.ThunderAnchors;
        for (var i = 0; i < anchors.Length; i++)
        {
            var real = (i + realOffset) % 2 == 0;
            if (!real && !isLie) continue;
            var placement = anchors[i].MulX(orientation).MulRot(orientation);
            if (real) kefka?.CastFromHelper(realLine, at: placement);
            else kefka?.CastFromHelper(Constants.ActionId.ThrummingThunderIII_FakeOmen, at: placement, animationLock: 1.1f);
        }
    }

    private void ResolveTrapStack(PartyRole holderRole, PartyRole? nextHolderRole, float nextDuration)
    {
        if (party.Get(holderRole) is not { } holder || !holder.IsAlive()) return;
        kefka?.CastFromHelper(UmadActions.DoubleTroubleTrapStack, holder);
        if (nextHolderRole is { } next && nextDuration > 0f)
            party.Get(next)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, nextDuration);
    }

    private static float ArrowHeading(TelePortentDirection dir) => dir switch
    {
        TelePortentDirection.Up => float.Pi,
        TelePortentDirection.Down => 0f,
        TelePortentDirection.Right => float.Pi / 2f,
        TelePortentDirection.Left => -float.Pi / 2f,
        _ => 0f,
    };

    private static ushort PrimaryArrowStatus(TelePortentDirection dir) => dir switch
    {
        TelePortentDirection.Up => Constants.StatusId.TelePortentUpPrimary,
        TelePortentDirection.Down => Constants.StatusId.TelePortentDownPrimary,
        TelePortentDirection.Right => Constants.StatusId.TelePortentRightPrimary,
        TelePortentDirection.Left => Constants.StatusId.TelePortentLeftPrimary,
        _ => Constants.StatusId.TelePortentUpPrimary,
    };

    private static ushort SecondaryArrowStatus(TelePortentDirection dir) => dir switch
    {
        TelePortentDirection.Up => Constants.StatusId.TelePortentUpSecondary,
        TelePortentDirection.Down => Constants.StatusId.TelePortentDownSecondary,
        TelePortentDirection.Right => Constants.StatusId.TelePortentRightSecondary,
        TelePortentDirection.Left => Constants.StatusId.TelePortentLeftSecondary,
        _ => Constants.StatusId.TelePortentUpSecondary,
    };
}