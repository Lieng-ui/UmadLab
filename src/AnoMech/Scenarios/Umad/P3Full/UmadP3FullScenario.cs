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
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Scenarios.Umad.P3LimitCut;
using static AnoMech.Scenarios.Umad.UmadConstants;
using static AnoMech.Scenarios.Umad.P3Full.UmadP3FullState;

namespace AnoMech.Scenarios.Umad.P3Full;

public sealed class UmadP3FullScenario : IScenario
{
    public string Name => "Full";
    public IPhase Phase => UmadZone.P3;
    // 20293 starts ~30.41s before P3 knockdown ends (matches LimitCut 108.67s - 7.30s - 70.96s).
    public float BgmSecondsAtStart => 30.41f;
    public float BgmStartDelaySeconds => state?.CountdownSeconds ?? 0f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP3FullAi()];
    public object SettingsOverrides => settingsWindow.Overrides;

    // 3把通关日志实测：击倒(0x306)结束至双神可选中为 7.30s / 7.28s / 7.30s
    public const float PreTargetableDurationSec = 7.30f;
    // 一运后半 Limit Cut 相对 T0 偏移：UmbraSmash(lc=8.00s) 对应日志 T0+78.96s
    public const float LimitCutBaseSec = 70.96f;
    // 二运黑洞相对 T0 偏移：Earthquake(bh=0.98s) 对应日志 T0+126.71s
    public const float BlackHoleBaseSec = 125.73f;
    public static bool SuppressNextCountdown;

    private static readonly (ushort Id, string Key)[] CloneTimelines =
    [
        (4575, "mon_sp/m0462/hide/mon_sp003"),
        (4576, "mon_sp/m0462/hide/mon_sp004"),
    ];

    private static readonly float[] PlaceCloneLc = [8.803f, 10.808f, 12.815f, 14.821f, 16.830f, 18.836f, 20.840f, 22.846f];
    private static readonly float[] CloneAppearLc = [8.892f, 10.898f, 12.904f, 14.910f, 16.919f, 18.925f, 20.929f, 22.933f];
    private static readonly float[] PrepareChargeLc = [30.853f, 31.075f, 31.298f, 31.521f, 31.745f, 31.968f, 32.191f, 32.415f];
    private static readonly float[] ChargeLc = [30.953f, 31.175f, 31.398f, 31.621f, 31.845f, 32.068f, 32.291f, 32.515f];

    private const float BlackSparkRadius = 1.45f;
    private const float BlackHoleAvoidRadius = 2.1f;
    private const float CleanseDelayAfterSpentCrust = 1.55f;
    private static readonly Placement CleanseSpot = new(new Vector3(0.000f, 0.000f, 4.000f), 0.000f);

    private readonly UmadP3FullSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP3FullState state = null!;
    private SimEnemy? kefka;
    private SimEnemy? chaos;
    private SimEnemy? exdeath;
    private readonly SimEnemy?[] cycloneHelpers = new SimEnemy?[8];
    private SimEventObject? fireCrystal;
    private SimEventObject? waterCrystal;
    private SimEventObject? windCrystal;
    private SimEventObject? earthCore;
    private Placement implosionFrom;
    private Placement bhImplosionFrom;
    private Vector3 umbraImpact;
    private List<Vector3>[] blackHolePositions = null!;
    private readonly float[] blackSparkCooldown = new float[8];
    private int primordialCrustsToResolve;
    private float cleanseCooldown;

    public UmadP3FullState? LastState { get; private set; }

    public void DrawSettings() => settingsWindow.Draw();

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(CloneTimelines, "UmadP3Full");
        instanceWorld.Party.LimitBreak.Set(3f);
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        QueueCyclonesForWinds(resolved);
        Towers.ResolveTower(world, resolved, ActionId.StompAMole, UmadActions.UnmitigatedImpact, minSoakers: 1);
        QueueCleansesForSpentCrusts(resolved);
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (lost is { StatusId: StatusId.Headwind or StatusId.Tailwind, Reason: StatusLossReason.Expired })
        {
            world.Events.Add(0.09f, () => chaos?.CastFromHelper(UmadActions.StrayGusts, lost.Holder, at: new Placement(WindCrystalPos, 0f)));
            return;
        }
        if (lost is { StatusId: StatusId.PrimordialCrust, Reason: StatusLossReason.Expired })
        {
            lost.Holder.RemoveStatus(StatusId.FirstInLine);
            lost.Holder.RemoveStatus(StatusId.SecondInLine);
            lost.Holder.RemoveStatus(StatusId.ThirdInLine);
            world.Events.Add(0.09f, () => chaos?.CastFromHelper(UmadActions.StrayEarth, lost.Holder, at: CleanseSpot));
        }
    }

    private Vector3 WindCrystalPos => OnCircle3(SpotHeading(state.WindSpot), 14f);

    private void At(float p3Time, Action action)
        => world.Events.Add(state.CountdownSeconds + p3Time, action);

    private void AtCombat(float combatTime, Action action)
        => At(PreTargetableDurationSec + combatTime, action);

    private void AtLc(float lcTime, Action action)
        => AtCombat(LimitCutBaseSec + lcTime, action);

    private void AtBh(float bhTime, Action action)
        => AtCombat(BlackHoleBaseSec + bhTime, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        var suppressCd = SuppressNextCountdown;
        SuppressNextCountdown = false;
        var countdown = suppressCd ? 0f : settingsWindow.Overrides.CountdownSeconds;
        state = new UmadP3FullState(world, settingsWindow.Overrides, countdown);
        LastState = state;
        fireCrystal = null;
        waterCrystal = null;
        windCrystal = null;
        earthCore = null;
        primordialCrustsToResolve = 0;
        cleanseCooldown = 0f;
        blackHolePositions = Enumerable.Range(0, 4).Select(GenerateBlackHolePositions).ToArray();
        Array.Clear(blackSparkCooldown);

        ScheduleCountdownLogs();

        if (selectedAi is { } idx && idx >= 0 && idx < AiStrats.Count)
            ((IScenarioAi<UmadP3FullState>)AiStrats[idx]).Run(state, world);

        SpawnActors();
        ScheduleOpenerAndBowels();
        ScheduleLimitCut();
        ScheduleBlackHoleAndStomp();
        ScheduleEnding();
    }

    public void Tick(float delta, float elapsed)
    {
        cleanseCooldown = MathF.Max(0f, cleanseCooldown - delta);
        if (cleanseCooldown == 0f && primordialCrustsToResolve > 0)
        {
            primordialCrustsToResolve--;
            cleanseCooldown = UmadActions.EarthquakeCleanse.Cast.AnimationLock;
            chaos?.CastFromHelper(UmadActions.EarthquakeCleanse, at: CleanseSpot);
        }

        if (blackHolePositions is null) return;
        var bhElapsed = elapsed - (state.CountdownSeconds + PreTargetableDurationSec + BlackHoleBaseSec);
        var wave = bhElapsed switch
        {
            > 28.17f and < 41.33f => 0,
            > 58.79f and < 75.07f => 1,
            > 92.95f and < 109.12f => 2,
            > 126.24f and < 139.83f => 3,
            _ => -1,
        };
        if (wave == -1) return;

        for (var slot = 0; slot < 8; slot++)
        {
            if (blackSparkCooldown[slot] > 0f)
            {
                blackSparkCooldown[slot] = MathF.Max(0f, blackSparkCooldown[slot] - delta);
                continue;
            }
            var member = party.Get(slot);
            if (member is null || !member.IsAlive()) continue;
            foreach (var bh in blackHolePositions[wave])
            {
                var distSq = member.Placement().DistanceSq(bh);
                if (distSq < BlackSparkRadius * BlackSparkRadius)
                {
                    blackSparkCooldown[slot] = 2.0f;
                    exdeath?.CastFromHelper(UmadActions.BlackSpark, member, at: new Placement(bh, 0f));
                    break;
                }
            }
        }
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
            BNpcBaseId: BNpcBaseId.KefkaP3,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Visibility: SpawnVisibility.Visible,
            LayoutId: BNpcLayoutId.KefkaP3,
            Placement: new Placement(Vector3.Zero, 0f)));

        chaos = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.ChaosP3,
            NameId: BNpcNameId.Chaos,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.HiddenUntilShown,
            LayoutId: BNpcLayoutId.ChaosP3,
            Placement: new Placement(new Vector3(-8f, 0f, 0f), 0f)));

        exdeath = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Exdeath,
            NameId: BNpcNameId.Exdeath,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.HiddenUntilShown,
            LayoutId: BNpcLayoutId.Exdeath,
            Placement: new Placement(new Vector3(8f, 0f, 0f), 0f)));

        for (var k = 0; k < 8; k++)
            state.Objects.Clones[k] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaCloneP3,
                NameId: BNpcNameId.Kefka,
                Level: 100,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.HiddenUntilShown,
                LayoutId: BNpcLayoutId.KefkaCloneP3,
                Placement: new Placement(Vector3.Zero, 0f)));

        for (var i = 0; i < 8; i++)
            cycloneHelpers[i] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Chaos,
                NameId: BNpcNameId.Chaos,
                Level: 1,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.HiddenUntilShown,
                Placement: new Placement(WindCrystalPos, 0f)));

        state.Objects.Kefka = kefka;
        state.Objects.Chaos = chaos;
        state.Objects.Exdeath = exdeath;
        state.BlackHole.ScenarioObjects.Chaos = chaos;
        state.BlackHole.ScenarioObjects.Exdeath = exdeath;
    }

    private void ScheduleOpenerAndBowels()
    {
        At(0.27f, () => kefka?.Cast(ActionId.KefkaP3Enter, animationLock: 3.1f));
        At(5.25f, () =>
        {
            chaos?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            exdeath?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        AtCombat(0.00f, () =>
        {
            chaos?.SetTargetable(true);
            exdeath?.SetTargetable(true);
            kefka?.PlayActionTimeline(ActionTimelineId.WarpStart);
        });
        AtCombat(0.14f, () =>
        {
            chaos?.Cast(ActionId.DecisiveBattle_Chaos, animationLock: 3.1f);
            exdeath?.Cast(ActionId.DecisiveBattle_Exdeath, animationLock: 3.1f);
        });
        AtCombat(2.22f, () =>
        {
            kefka?.SetPosition(new Placement(new Vector3(0f, 0f, -10f), 0f));
            kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });
        AtCombat(3.14f, ResolveDecisiveBattle);
        AtCombat(3.40f, () =>
        {
            chaos?.Follow(party.Get(PartyRole.OffTank));
            exdeath?.Follow(party.Get(PartyRole.MainTank));
        });
        AtCombat(4.36f, () => kefka?.Cast(ActionId.KefkaRest, animationLock: 2.1f));

        foreach (var at in new[]
        {
            6.25f, 9.28f, 12.31f, 15.34f, 25.60f, 28.63f, 31.66f, 34.69f, 37.72f, 40.75f,
            43.78f, 46.81f, 49.84f, 52.87f, 55.90f, 68.00f, 71.03f, 74.06f, 77.09f,
            89.03f, 92.06f, 97.12f, 100.15f, 103.17f, 108.25f, 111.27f, 114.30f
        })
            AtCombat(at, () => chaos?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.OffTank)));
        AtCombat(120.34f, () => chaos?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));

        foreach (var at in new[]
        {
            6.60f, 9.63f, 12.66f, 15.69f, 25.90f, 28.93f, 31.96f, 56.50f, 59.53f, 62.56f,
            65.59f, 68.62f, 71.65f, 74.68f, 77.71f,
            91.34f, 97.43f, 100.46f, 103.49f, 112.56f
        })
            AtCombat(at, () => exdeath?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.MainTank)));
        foreach (var at in new[] { 118.61f, 121.63f })
            AtCombat(at, () => exdeath?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));

        AtCombat(17.55f, () => chaos?.Cast(UmadActions.BowelsOfAgony));
        AtCombat(22.54f, ResolveBowelsOfAgony);

        AtCombat(34.68f, StripDecisiveBattle);
        AtCombat(34.72f, () =>
        {
            exdeath?.Follow();
            exdeath?.Cast(UmadActions.ThunderIIILarge);
        });

        AtCombat(41.62f, () => ResolveElementDebuffs(isShort: true));
        AtCombat(42.65f, () => ResolveElementCrystal(isShort: true));

        AtCombat(45.85f, () => exdeath?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        AtCombat(50.92f, () => ResolveThunderTb(requireSwap: false));
        AtCombat(53.95f, () => ResolveThunderTb(requireSwap: false));
        AtCombat(54.50f, () => exdeath?.Follow(party.Get(PartyRole.MainTank)));

        AtCombat(58.68f, () => kefka?.Cast(ActionId.KefkaTransform, animationLock: 3.1f));
        AtCombat(62.73f, () =>
        {
            kefka?.AddStatusParam(StatusId.KefkaTrance, 0x1FF);
            kefka?.SetAnimationState(0, 1);
        });

        AtCombat(58.77f, () =>
        {
            if (chaos is null) return;
            chaos.Follow();
            implosionFrom = chaos.Placement();
            chaos.Cast(state.ImplosionAttack, animationLock: 6.1f);
        });
        for (var i = 0; i < 2; i++)
        {
            var rotAdj = i * MathF.PI + (state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f);
            AtCombat(64.50f, () => chaos?.CastFromHelper(UmadActions.ImplosionShockwave, at: implosionFrom.AddToRotation(rotAdj)));
            AtCombat(66.50f, () => chaos?.CastFromHelper(UmadActions.ImplosionShockwave, at: implosionFrom.AddToRotation(rotAdj + MathF.PI / 2f)));
        }
        AtCombat(67.20f, () => chaos?.Follow(party.Get(PartyRole.OffTank)));

        AtCombat(68.66f, () => ResolveElementDebuffs(isShort: false));
        AtCombat(69.68f, () => ResolveElementCrystal(isShort: false));
    }

    private void ScheduleLimitCut()
    {
        AtLc(2.0f, () => party.LimitBreak.Set(3f));
        AtLc(14.0f, () => party.LimitBreak.Set(3f));
        AtLc(6.915f, () =>
        {
            kefka?.ActorControl.Transform(BNpcStateId.KefkaLimitCut);
            kefka?.RemoveStatus(StatusId.KefkaTrance);
            kefka?.AddStatusParam(StatusId.KefkaTrance, 0x22B);
        });

        AtLc(7.80f, () => chaos?.Follow());
        AtLc(7.93f, () => exdeath?.Follow());
        AtLc(8.00f, StartUmbraSmash);
        AtLc(8.17f, () => exdeath?.Cast(UmadActions.VacuumWave));

        for (var k = 0; k < 8; k++)
        {
            var clone = k;
            AtLc(PlaceCloneLc[k], () => PlaceClone(clone));
            AtLc(CloneAppearLc[k], () => CloneAppear(clone));
        }

        AtLc(14.799f, ChaosLands);
        AtLc(17.56f, () => exdeath?.Follow(party.Get(PartyRole.MainTank)));
        AtLc(18.83f, AttachNumbers);
        AtLc(20.934f, () => windCrystal?.Despawn());
        AtLc(21.379f, () =>
        {
            chaos?.Cast(ActionId.Aetherlink_Chaos, animationLock: 3.1f);
            exdeath?.Cast(ActionId.Aetherlink_Exdeath, animationLock: 3.1f);
        });

        if (state.EnableCheatSheet)
            AtLc(19.20f, PrintLimitCutCheatSheet);

        for (var sec = 5; sec >= 1; sec--)
        {
            var remaining = sec;
            if (state.EnableCheatSheet)
                AtLc(ChargeLc[0] - sec, () => Plugin.ChatGui?.Print($"麻将判定倒数 {remaining} 秒..."));
        }

        for (var k = 0; k < 8; k++)
        {
            var clone = k;
            AtLc(PrepareChargeLc[k], () => PrepareCharge(clone));
            AtLc(ChargeLc[k], () => ResolveCharge(clone));
        }

        AtLc(33.29f, () => exdeath?.Follow());
        AtLc(33.49f, () => exdeath?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        AtLc(38.57f, () => ResolveThunderTb(requireSwap: true));
        AtLc(41.60f, () => ResolveThunderTb(requireSwap: true));
        AtLc(42.10f, () => exdeath?.Follow(party.Get(PartyRole.MainTank)));

        AtLc(43.30f, () =>
        {
            chaos?.Follow();
            exdeath?.Follow();
        });
        AtLc(43.50f, () =>
        {
            chaos?.Cast(ActionId.DecisiveBattle_Chaos, animationLock: 3.1f);
            exdeath?.Cast(ActionId.DecisiveBattle_Exdeath, animationLock: 3.1f);
        });
        AtLc(46.49f, ResolveDecisiveBattle);
        AtLc(46.69f, () =>
        {
            chaos?.Follow(party.Get(PartyRole.MainTank));
            exdeath?.Follow(party.Get(PartyRole.OffTank));
        });
        AtLc(50.581f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        AtLc(50.746f, () =>
        {
            exdeath?.Follow();
            exdeath?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f);
        });
        AtLc(55.83f, () => ResolveThunderTb(requireSwap: true));
        AtLc(58.86f, () => ResolveThunderTb(requireSwap: true));
        AtLc(59.40f, () => exdeath?.Follow(party.Get(PartyRole.OffTank)));
    }

    private void ScheduleBlackHoleAndStomp()
    {
        var bh = state.BlackHole;
        var kefkaOrigin = new Placement(Vector3.Zero, 0f);

        // Director updates & arena effect for Black Hole / Stomp-a-Mole
        AtBh(5.88f, () => world.Map.DirectorUpdate(0x80000027U, 0x1BU, 0x2U, 0x1BDBU, 0x40004141U));
        AtBh(21.34f, () => world.Map.DirectorUpdate(0x80000027U, 0x1CU, 0x2U, 0x1BDBU, 0x40004141U));
        AtBh(77.20f, () => world.Map.DirectorUpdate(0x80000027U, 0x1DU, 0x2U, 0x1BDBU, 0x40004141U));
        AtBh(145.43f, () => world.Map.DirectorUpdate(0x80000027U, 0x1EU, 0x2U, 0x1BDBU, 0x40004141U));
        AtBh(156.68f, () => world.Map.AddEffect(packetFlags: 0x00020001U, index: (byte)0x22));

        // Earthquake & Accretion cleanses
        AtBh(0.98f, () =>
        {
            chaos?.Cast(ActionId.Earthquake, animationLock: 3.1f);
            chaos?.CastFromHelper(ActionId.Earthquake_Visual, at: new Placement(Vector3.Zero, 0f), animationLock: 1.1f);
        });
        AtBh(6.04f, () => earthCore = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.EarthCore,
            LayoutId = EObjId.EarthCoreLayoutId,
            TargetableStatus = 5,
            Arg2 = EObjId.EarthCoreArg2,
            Placement = new Placement(new Vector3(0f, 0f, 4f), 0f),
        }));
        AtBh(140.13f, () =>
        {
            earthCore?.Despawn();
            earthCore = null;
        });

        AtBh(8.37f, () =>
        {
            bh.Roles.Get(0)?.AddStatus(StatusId.FirstInLine);
            bh.Roles.Get(0)?.AddStatus(StatusId.PrimordialCrust, 72);
            bh.Roles.Get(1)?.AddStatus(StatusId.SecondInLine);
            bh.Roles.Get(1)?.AddStatus(StatusId.PrimordialCrust, 106);
            bh.Roles.Get(2)?.AddStatus(StatusId.ThirdInLine);
            bh.Roles.Get(2)?.AddStatus(StatusId.PrimordialCrust, 139);
            bh.Roles.Get(3)?.AddStatus(StatusId.FirstInLine);
            bh.Roles.Get(3)?.AddStatus(StatusId.PrimordialCrust, 72);
            bh.Roles.Get(3)?.AddStatus(StatusId.Accretion, 14.000f);
            bh.Roles.Get(4)?.AddStatus(StatusId.FirstInLine);
            bh.Roles.Get(4)?.AddStatus(StatusId.PrimordialCrust, 72);
            bh.Roles.Get(5)?.AddStatus(StatusId.SecondInLine);
            bh.Roles.Get(5)?.AddStatus(StatusId.PrimordialCrust, 106);
            bh.Roles.Get(6)?.AddStatus(StatusId.ThirdInLine);
            bh.Roles.Get(6)?.AddStatus(StatusId.PrimordialCrust, 139);
            bh.Roles.Get(7)?.AddStatus(StatusId.SecondInLine);
            bh.Roles.Get(7)?.AddStatus(StatusId.PrimordialCrust, 106);
            bh.Roles.Get(7)?.AddStatus(StatusId.Accretion, 14.000f);
        });
        AtBh(12.38f, () => bh.Roles.Get(7)?.RemoveStatus(StatusId.Accretion));
        AtBh(13.40f, () => chaos?.CastFromHelper(UmadActions.EarthquakeCleanse, bh.Roles.Get(7), at: CleanseSpot));
        AtBh(16.30f, () => bh.Roles.Get(3)?.RemoveStatus(StatusId.Accretion));
        AtBh(17.32f, () => chaos?.CastFromHelper(UmadActions.EarthquakeCleanse, bh.Roles.Get(3), at: CleanseSpot));

        // Hold Chaos centred through waves 1-3 when MT is a bot (matching clear logs); release for Wave 4 LookUponSplit
        AtBh(14.0f, () =>
        {
            if (party.PlayerRole == PartyRole.MainTank) return;
            chaos?.Follow();
            if (party.Get(PartyRole.MainTank) is { } mt) chaos?.Face(mt);
        });
        AtBh(113.0f, () =>
        {
            if (party.PlayerRole == PartyRole.MainTank) return;
            if (party.Get(PartyRole.MainTank) is { } mt) chaos?.Face(mt);
        });
        AtBh(133.5f, () => chaos?.Follow(party.Get(PartyRole.MainTank)));

        // Chaos autos & casts during Black Hole + Stomp-a-Mole
        foreach (var at in new[]
        {
            12.07f, 15.09f, 18.13f, 21.16f, 24.19f, 27.22f, 30.25f, 33.29f, 36.34f, 39.37f, 42.41f,
            53.48f, 56.51f, 59.54f, 80.90f, 83.94f, 86.97f, 90.00f, 93.02f, 124.41f, 141.47f, 144.50f, 152.53f, 155.57f
        })
            AtBh(at, () => chaos?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));

        AtBh(32.40f, () => chaos?.Cast(ActionId.Aetherlink_Chaos, animationLock: 3.1f));
        ScheduleBhEdict(45.58f);
        ScheduleBhEdict(72.87f);
        AtBh(99.18f, () => chaos?.Cast(ActionId.Aetherlink_Chaos, animationLock: 3.1f));

        AtBh(113.38f, () =>
        {
            if (chaos is null) return;
            bhImplosionFrom = chaos.Placement();
            chaos.Cast(bh.ImplosionAttack, animationLock: 6.1f);
        });
        for (var i = 0; i < 2; i++)
        {
            var rotAdj = i * MathF.PI + (bh.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f);
            AtBh(119.09f, () => chaos?.CastFromHelper(UmadActions.ImplosionShockwave, at: bhImplosionFrom.AddToRotation(rotAdj)));
            AtBh(121.11f, () => chaos?.CastFromHelper(UmadActions.ImplosionShockwave, at: bhImplosionFrom.AddToRotation(rotAdj + MathF.PI / 2f)));
        }

        AtBh(147.09f, () =>
        {
            bh.StackTargets.Get(0)?.AttachLockonVfx(LockonId.Stack);
            chaos?.Cast(ActionId.KnockDown_Cast, animationLock: 3.1f);
        });
        AtBh(152.58f, () => bh.StackTargets.Get(1)?.AttachLockonVfx(LockonId.Stack));
        AtBh(157.22f, () => chaos?.Cast(ActionId.BigBang_Cast, animationLock: 3.1f));

        var knockFrom = new Placement(new Vector3(-4.452f, 0.200f, -5.299f), -2.220f);
        var bigBangSpot0 = Vector3.Zero;
        var bigBangSpot1 = Vector3.Zero;
        AtBh(152.13f, () =>
        {
            if (bh.StackTargets.Get(0) is { } t0)
            {
                bigBangSpot0 = t0.Position;
                chaos?.CastFromHelper(UmadActions.KnockDown, t0, at: knockFrom);
            }
        });
        AtBh(157.67f, () =>
        {
            if (bh.StackTargets.Get(1) is { } t1)
            {
                bigBangSpot1 = t1.Position;
                chaos?.CastFromHelper(UmadActions.KnockDown, t1, at: knockFrom);
            }
        });
        AtBh(162.31f, () =>
        {
            chaos?.CastFromHelper(UmadActions.BigBang, at: new Placement(bigBangSpot0, 0f));
            chaos?.CastFromHelper(UmadActions.BigBang, at: new Placement(bigBangSpot1, 0f));
        });

        // Exdeath autos & casts during Black Hole + Stomp-a-Mole
        foreach (var at in new[]
        {
            12.07f, 15.09f, 18.13f, 21.16f, 27.22f, 30.25f, 33.29f, 36.34f,
            44.37f, 47.41f, 50.45f, 53.48f, 56.51f, 59.54f, 77.78f,
            85.85f, 88.89f, 91.91f, 122.45f, 125.48f, 141.51f, 150.66f, 153.69f
        })
            AtBh(at, () => exdeath?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));

        AtBh(22.18f, () => exdeath?.Cast(ActionId.BlackHole, animationLock: 3.1f));
        AtBh(32.40f, () => exdeath?.Cast(ActionId.Aetherlink_Exdeath, animationLock: 3.1f));
        AtBh(37.59f, () => exdeath?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        ScheduleBhThunder(42.63f, ThunderIIIPlanning.Roles(bh.ThunderSet1).Second is not null);
        AtBh(78.85f, () => exdeath?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        ScheduleBhThunder(83.94f, ThunderIIIPlanning.Roles(bh.ThunderSet2).Second is not null);
        AtBh(99.18f, () => exdeath?.Cast(ActionId.Aetherlink_Exdeath, animationLock: 3.1f));
        AtBh(113.38f, () => exdeath?.Cast(UmadActions.WhiteHole));
        AtBh(143.61f, () => exdeath?.Cast(ActionId.BlizzardIII_Cast, animationLock: 3.1f));
        var blizzardCheckPos = new Vector3?[8];
        AtBh(156.95f, () => exdeath?.Cast(UmadActions.BlizzardIIIRaidwide));
        AtBh(160.60f, () =>
        {
            for (var i = 0; i < 8; i++)
                blizzardCheckPos[i] = party.Get(i) is { } m && m.IsAlive() ? m.Position : null;
        });
        AtBh(160.95f, () =>
        {
            for (var i = 0; i < 8; i++)
            {
                if (party.Get(i) is not { } m || !m.IsAlive()) continue;
                if (blizzardCheckPos[i] is not { } prev) continue;
                var dx = m.Position.X - prev.X;
                var dz = m.Position.Z - prev.Z;
                if (dx * dx + dz * dz <= 0.04f)
                    m.AddStatus(StatusId.DeepFreeze, 3.0f);
            }
        });

        // Big Kefka (Max) teleports, Slap Happy, Look Upon Me and Despair, Stomp-a-Mole
        AtBh(5.88f, () =>
        {
            kefka?.RemoveStatus(StatusId.KefkaTrance);
            kefka?.AddStatus(StatusId.Max, stacks: (ushort)506, overrideStacks: true);
            kefka?.SetModelState((byte)0x05);
            kefka?.SetPosition(kefkaOrigin);
        });
        AtBh(6.05f, () => kefka?.PlayActionTimeline(TimelineId.KefkaP3Show));

        AtBh(12.21f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        AtBh(14.16f, () => kefka?.SetPosition(bh.KefkaPosition[0].Apply(kefkaOrigin)));
        AtBh(14.33f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        AtBh(16.39f, () => kefka?.Cast(bh.SlapAttacks[0], animationLock: 6.1f));

        AtBh(42.64f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        AtBh(44.60f, () => kefka?.SetPosition(bh.KefkaPosition[1].Apply(kefkaOrigin)));
        AtBh(44.76f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        AtBh(46.83f, () => kefka?.Cast(bh.SlapAttacks[1], animationLock: 6.1f));

        AtBh(70.06f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        AtBh(72.02f, () => kefka?.SetPosition(bh.KefkaPosition[2].Apply(kefkaOrigin)));
        AtBh(72.21f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        AtBh(74.25f, () => kefka?.Cast(ActionId.LookUponMeAndDespair, animationLock: 3.1f));
        AtBh(74.29f, () => kefka?.CastFromHelper(UmadActions.LookUponMeAndDespair, bh.KefkaPosition[2].Apply(new Vector3(0f, 0f, -50f)), at: bh.KefkaPosition[2].Apply(kefkaOrigin)));
        AtBh(79.38f, () => kefka?.SetModelState((byte)0x07));
        AtBh(81.39f, () => kefka?.Cast(ActionId.StandUp_ToWall, animationLock: 4.1f));
        AtBh(82.20f, () => kefka?.SetModelState((byte)0x05));

        AtBh(106.61f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        AtBh(108.56f, () => kefka?.SetPosition(bh.KefkaPosition[3].Apply(kefkaOrigin)));
        AtBh(108.75f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        AtBh(114.81f, () => kefka?.Cast(bh.SlapAttacks[2], animationLock: 6.1f));

        AtBh(127.98f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        AtBh(130.02f, () => kefka?.SetPosition(bh.KefkaPosition[4].Apply(kefkaOrigin)));
        AtBh(130.18f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        AtBh(132.26f, () => kefka?.Cast(ActionId.LookUponMeAndDespair2, animationLock: 3.1f));
        AtBh(132.35f, () => kefka?.CastFromHelper(UmadActions.LookUponMeAndDespair, bh.KefkaPosition[4].Apply(new Vector3(0f, 0f, -50f)), at: bh.KefkaPosition[4].Apply(kefkaOrigin)));
        AtBh(137.40f, () => kefka?.SetModelState((byte)0x07));
        AtBh(139.41f, () => kefka?.Cast(ActionId.StandUp_Levitate, animationLock: 4.1f));
        AtBh(140.22f, () => kefka?.SetModelState((byte)0x06));
        AtBh(145.52f, () => kefka?.Cast(ActionId.StompAMole_Cast, animationLock: 8.1f));
        AtBh(152.13f, () => kefka?.SetModelState((byte)0x05));

        var center = new Placement(Vector3.Zero, 0f);
        AtBh(23.25f, () => kefka?.CastFromHelper(UmadActions.SlapHappyFinalSlap, at: center));
        AtBh(53.71f, () => kefka?.CastFromHelper(UmadActions.SlapHappyFinalSlap, at: center));
        AtBh(121.69f, () => kefka?.CastFromHelper(UmadActions.SlapHappyFinalSlap, at: center));

        for (var i = 0; i < 3; i++)
        {
            ScheduleSlapAttack(22.14f, 0, 0, i);
            ScheduleSlapAttack(52.59f, 1, 1, i);
            ScheduleSlapAttack(120.57f, 2, 3, i);
            ScheduleSlapCone(24.81f, 0, i);
            ScheduleSlapCone(55.26f, 1, i);
            ScheduleSlapCone(123.25f, 2, i);
        }

        // Passive & Active Black Holes + Obstacles
        for (var i = 0; i < 8; i++)
        {
            var idx = i + 3;
            SimEnemy? hole = null;
            AtBh(25.17f, () => hole = SpawnBlackHole(blackHolePositions[0][idx]));
            AtBh(40.41f, () => hole?.FadeOut());
            AtBh(55.79f, () => hole = SpawnBlackHole(blackHolePositions[1][idx]));
            AtBh(74.02f, () => hole?.FadeOut());
            AtBh(89.95f, () => hole = SpawnBlackHole(blackHolePositions[2][idx]));
            AtBh(108.27f, () => hole?.FadeOut());
            AtBh(123.83f, () => hole = SpawnBlackHole(blackHolePositions[3][idx]));
            AtBh(138.79f, () => hole?.FadeOut());
        }

        ScheduleActiveBlackHole(blackHolePositions[0][2], 25.17f, 26.00f, 32.27f, 1);
        ScheduleActiveBlackHole(blackHolePositions[0][1], 25.17f, 32.27f, 39.33f, 1);
        ScheduleActiveBlackHole(blackHolePositions[0][0], 25.17f, 32.27f, 39.33f, 1);
        for (var i = 0; i < 3; i++)
        {
            ScheduleActiveBlackHole(blackHolePositions[1][i], 55.70f, 56.36f, 62.83f, 3);
            ScheduleActiveBlackHole(blackHolePositions[2][i], 89.95f, 90.77f, 97.12f, 3);
        }
        ScheduleActiveBlackHole(blackHolePositions[3][0], 123.83f, 124.33f, 130.60f, 1);
        ScheduleActiveBlackHole(blackHolePositions[3][1], 123.83f, 124.33f, 130.60f, 1);
        ScheduleActiveBlackHole(blackHolePositions[3][2], 123.83f, 130.60f, 137.67f, 1);

        ScheduleBlackHoleObstacles(0, 26.67f, 41.33f);
        ScheduleBlackHoleObstacles(1, 57.29f, 75.07f);
        ScheduleBlackHoleObstacles(2, 91.45f, 109.12f);
        ScheduleBlackHoleObstacles(3, 125.33f, 139.83f);

        // Stomp-a-Mole Blizzard III puddles & 4 Stomp towers
        for (var i = 0; i < 8; i++)
        {
            var role = (PartyRole)i;
            AtBh(146.68f, () =>
            {
                if (party.Get(role) is { } m)
                    exdeath?.CastFromHelper(UmadActions.BlizzardIII, m.Position, at: new Placement(new Vector3(-9.9f, 0f, 9.9f), 0f));
            });
            AtBh(149.73f, () =>
            {
                if (party.Get(role) is { } m)
                    exdeath?.CastFromHelper(UmadActions.BlizzardIII, m.Position, at: new Placement(new Vector3(0f, 0f, -10f), 0f));
            });
        }

        ScheduleStomp(150.71f, -1);
        ScheduleStomp(152.00f, 1);
        ScheduleStomp(153.29f, -1);
        ScheduleStomp(154.63f, 1);

        ScheduleBlackHolePlayerCallouts();
    }


    private void ScheduleEnding()
    {
        const float bigBangAt = BlackHoleBaseSec + 157.22f;
        var chaosKillAt = bigBangAt + state.ChaosKillDelayAfterBigBangSec;
        var exdeathKillAt = chaosKillAt + state.ExdeathKillDelayAfterChaosSec;

        for (var t = bigBangAt + 3.10f; t < chaosKillAt - 0.5f; t += 3.03f)
        {
            AtCombat(t, () => chaos?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
            AtCombat(t + 0.35f, () => exdeath?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        }

        AtCombat(chaosKillAt, () =>
        {
            chaos?.SetTargetable(false);
            chaos?.Defeat(corpseFadeDelay: 3.0f);
        });

        AtCombat(chaosKillAt + 0.09f, () =>
        {
            exdeath?.Follow();
            exdeath?.Cast(ActionId.ExdeathEnrageMeteor, animationLock: 3.1f);
        });

        AtCombat(exdeathKillAt, () =>
        {
            exdeath?.CancelCast(CastCancelReason.Interrupted);
            exdeath?.SetTargetable(false);
            exdeath?.Defeat(corpseFadeDelay: 2.0f);
            kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2);
        });
    }

    private SimEnemy? SpawnBlackHole(Vector3 pos) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: BNpcBaseId.BlackHole,
        NameId: BNpcNameId.BlackHole,
        Level: 100,
        Targetable: false,
        EnemyList: EnemyListMode.Never,
        Visibility: SpawnVisibility.Visible,
        LayoutId: BNpcLayoutId.BlackHole,
        Placement: FacingCentre(pos)));

    private void ScheduleBhEdict(float bhTime)
    {
        AtBh(bhTime - 0.5f, () =>
        {
            chaos?.Follow();
            chaos?.Face(state.BlackHole.EdictTargets.Get(0));
        });
        AtBh(bhTime, () => chaos?.Cast(UmadActions.DamningEdict));
        AtBh(bhTime + 6.0f, () =>
        {
            if (party.PlayerRole == PartyRole.MainTank)
                chaos?.Follow(party.Get(PartyRole.MainTank));
            else if (party.Get(PartyRole.MainTank) is { } mt)
                chaos?.Face(mt);
        });
    }

    private void ScheduleBhThunder(float bhTime, bool requireSwap)
    {
        AtBh(bhTime - 0.2f, () => exdeath?.Follow());
        AtBh(bhTime, () => ResolveThunderTb(requireSwap));
        AtBh(bhTime + 3.0f, () => ResolveThunderTb(requireSwap));
        AtBh(bhTime + 3.5f, () => exdeath?.Follow(party.Get(PartyRole.OffTank)));
    }

    private void ScheduleSlapAttack(float bhTime, int slapIndex, int kefkaIndex, int row)
    {
        var bh = state.BlackHole;
        var mul = bh.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left ? -1 : 1;
        var pos = bh.KefkaPosition[kefkaIndex].Apply(new Placement(new Vector3(10 * mul, 0f, -10 + row * 10), 0f));
        AtBh(bhTime + row * 0.65f, () => kefka?.CastFromHelper(UmadActions.SlapHappySlap, at: pos));
    }

    private void ScheduleSlapCone(float bhTime, int index, int row)
    {
        var targets = state.BlackHole.ConeTargets[index];
        if (row >= targets.List.Length) return;
        var target = targets.Get(row);
        var action = targets.List.Length == 1 ? UmadActions.ShockingImpact : UmadActions.ShockwaveCone;
        AtBh(bhTime, () => kefka?.CastFromHelper(action, LiveConeTarget(target), at: new Placement(Vector3.Zero, 0f)));
    }

    private SimCharacter? LiveConeTarget(SimCharacter? target)
    {
        if (target is null || target.IsAlive() || target is not ISimPartyMember deadMember) return target;
        return party.ActiveMembers()
            .FirstOrDefault(m => m is ISimPartyMember alive && alive.Role.IsTank() == deadMember.Role.IsTank() && alive.Role.IsDps() == deadMember.Role.IsDps())
            ?? target;
    }

    private List<Vector3> GenerateBlackHolePositions(int wave)
    {
        var bh = state.BlackHole;
        var inner = new Vector3(0f, 0f, -9f);
        var mid = new Vector3(bh.MiniBlackHoleChirality * 5.17f, 0f, 12.47f);
        var outer = new Vector3(0f, 0f, -17f);
        var passive = new List<Vector3>();
        var active = new List<Vector3>();
        for (var i = 0; i < 4; i++)
        {
            var dir = Direction.Cardinal[i].Rotate(bh.MiniBlackHoleInitialAngle + wave);
            passive.Add(dir.Apply(inner));
            passive.Add(dir.Apply(mid));
            if (i > 0)
            {
                var activeDir = bh.BlackHoleDirections[wave].Rotate(2 * i);
                active.Add(activeDir.Apply(outer));
            }
        }
        return active.Concat(passive).ToList();
    }

    private void ScheduleActiveBlackHole(Vector3 pos, float spawnTime, float tetherTime, float firstShotTime, int numberOfShots)
    {
        SimEnemy? hole = null;
        SimTether? tether = null;
        AtBh(spawnTime, () => hole = SpawnBlackHole(pos));
        if (tetherTime - spawnTime <= 1.0f)
            AtBh(spawnTime + 0.05f, () => { if (hole != null) ActLogBridge.EmitBlackHoleSpawnExtra(hole.ActEntityId, world.Coordinates.ToGlobal(pos), TetherId.GrabbyTether); });
        AtBh(tetherTime, () => tether = world.Tether(hole!, End.Passable(), TetherId.GrabbyTether));

        const float fadeOutDelay = 1.05f;
        const float shootCooldown = 5.1f;
        for (var i = 0; i < numberOfShots; i++)
        {
            var shotTime = firstShotTime + i * shootCooldown;
            AtBh(shotTime - 0.1f, () =>
            {
                if (hole is { } h && tether?.B is { } holder)
                    h.SetRotation(h.Placement().Face(holder.Position).Rotation);
            });
            AtBh(shotTime, () => hole?.Cast(UmadActions.Nothingness, tether?.B));
        }
        AtBh(firstShotTime + shootCooldown * (numberOfShots - 1), () => tether?.Despawn());
        AtBh(firstShotTime + shootCooldown * (numberOfShots - 1) + fadeOutDelay, () => hole?.FadeOut());
    }

    private void ScheduleBlackHoleObstacles(int wave, float avoidStart, float avoidEnd)
    {
        AtBh(avoidStart, () =>
        {
            foreach (var bhPos in blackHolePositions[wave])
                world.Obstacles.Add(new CircleObstacle(new Vector2(bhPos.X, bhPos.Z), BlackHoleAvoidRadius));
        });
        AtBh(avoidEnd, () => world.Obstacles.Clear());
    }

    private void ScheduleStomp(float bhTime, int mul)
        => AtBh(bhTime, () => kefka?.CastFromHelper(UmadActions.StompAMole, at: state.BlackHole.KefkaPosition[4].Apply(new Placement(new Vector3(mul * 10, 0f, 0f), 0f))));

    private static Placement FacingCentre(Vector3 position) => new Placement(position, 0f).Face(Vector3.Zero);

    private void QueueCleansesForSpentCrusts(ActionResolved resolved)
    {
        if (resolved.Action.ActionId != ActionId.Nothingness) return;
        var spent = resolved.Hits.Count(h => h.HasStatus(StatusId.MeanestExistence)
                                             && !h.HasStatus(StatusId.PrimordialCrust)
                                             && !resolved.Killed.Contains(h));
        if (spent > 0)
            world.Events.Add(CleanseDelayAfterSpentCrust, () => primordialCrustsToResolve += spent);
    }

    private void ResolveDecisiveBattle()
    {
        chaos?.AddStatus(StatusId.EpicVillain);
        exdeath?.AddStatus(StatusId.FatedVillain);
        if (chaos is null || exdeath is null) return;
        var ordered = party.ActiveMembers()
            .Where(m => m.IsAlive())
            .OrderBy(m => Vector3.DistanceSquared(m.Position, chaos.Position) - Vector3.DistanceSquared(m.Position, exdeath.Position))
            .ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].AddStatus(i < 4 ? StatusId.EpicHero : StatusId.FatedHero);
    }

    private void StripDecisiveBattle()
    {
        chaos?.RemoveStatus(StatusId.EpicVillain);
        exdeath?.RemoveStatus(StatusId.FatedVillain);
        party.ForEachActive(m =>
        {
            m.RemoveStatus(StatusId.EpicHero);
            m.RemoveStatus(StatusId.FatedHero);
        });
    }

    private void ResolveBowelsOfAgony()
    {
        SpawnCrystals();

        var fireDur = state.FireFirst ? 19.0f : 46.0f;
        var waterDur = state.FireFirst ? 46.0f : 19.0f;

        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(state.Winds[role] == Wind.Headwind ? StatusId.Headwind : StatusId.Tailwind, 68.0f);
            if (state.HasFire(role)) member.AddStatus(StatusId.ChaoticBlaze, fireDur);
            if (state.HasWater(role)) member.AddStatus(StatusId.ChaoticTsunami, waterDur);
        }
    }

    private void SpawnCrystals()
    {
        var firePos = OnCircle3(SpotHeading(state.FireSpot), 14f);
        var waterPos = OnCircle3(SpotHeading(state.WaterSpot), 14f);
        var windPos = WindCrystalPos;

        fireCrystal = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.FireCrystal,
            TargetableStatus = 5,
            Placement = new Placement(firePos, SpotHeading(state.FireSpot) + MathF.PI),
        });
        waterCrystal = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.WaterCrystal,
            TargetableStatus = 5,
            Placement = new Placement(waterPos, SpotHeading(state.WaterSpot) + MathF.PI),
        });
        var windLayout = state.WindSpot switch
        {
            1 => EObjId.WindCrystalSeLayoutId,
            3 => EObjId.WindCrystalNeLayoutId,
            5 => EObjId.WindCrystalNwLayoutId,
            _ => EObjId.WindCrystalSwLayoutId,
        };
        windCrystal = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.WindCrystal,
            LayoutId = windLayout,
            TargetableStatus = 5,
            Arg2 = EObjId.WindCrystalArg2,
            Placement = new Placement(windPos, SpotHeading(state.WindSpot) + MathF.PI),
        });
    }

    private void ResolveElementDebuffs(bool isShort)
    {
        var isFire = isShort ? state.FireFirst : !state.FireFirst;
        var supRole = isFire ? state.FireSupport : state.WaterSupport;
        var dpsRole = isFire ? state.FireDps : state.WaterDps;
        var action = isFire ? UmadActions.ChaoticBlaze : UmadActions.ChaoticTsunami;

        if (party.Get(supRole) is { } sup && sup.IsAlive())
            chaos?.CastFromHelper(action, sup, at: sup.Placement());
        if (party.Get(dpsRole) is { } dps && dps.IsAlive())
            chaos?.CastFromHelper(action, dps, at: dps.Placement());
    }

    private void ResolveElementCrystal(bool isShort)
    {
        var isFire = isShort ? state.FireFirst : !state.FireFirst;
        var spot = isFire ? state.FireSpot : state.WaterSpot;
        var crystalPos = OnCircle3(SpotHeading(spot), 14f);
        var action = isFire ? UmadActions.BlazeCrystal : UmadActions.TsunamiCrystal;

        var closest2 = party.Find.ClosestN(crystalPos, 2);
        for (var i = 0; i < closest2.Count; i++)
            chaos?.CastFromHelper(action, closest2[i], at: new Placement(crystalPos, 0f));

        if (isFire)
        {
            fireCrystal?.FadeOut();
            fireCrystal = null;
        }
        else
        {
            waterCrystal?.FadeOut();
            waterCrystal = null;
        }
    }

    private void ResolveThunderTb(bool requireSwap)
    {
        if (exdeath is null) return;
        exdeath.CastFromHelper(requireSwap ? UmadActions.ThunderIII : UmadActions.ThunderIIISolo, party.Find.Closest(exdeath.Position));
    }

    private void StartUmbraSmash()
    {
        if (chaos is null) return;
        var bait = party.Find.Farest(chaos.Position);
        umbraImpact = bait?.Position ?? chaos.Position;
        chaos.Cast(UmadActions.UmbraSmash, umbraImpact);
    }

    private void ChaosLands()
    {
        if (chaos is null) return;
        chaos.SetPosition(new Placement(umbraImpact, chaos.Rotation));
        chaos.Follow(party.Get(PartyRole.OffTank));
    }

    private void PlaceClone(int k)
    {
        if (state.Objects.Clones[k] is not { } clone) return;
        var spot = state.PlacementSpot(k);
        clone.SetPosition(new Placement(SpotPosition(spot), SpotHeading(spot) + MathF.PI));
        if (k == 0) kefka?.SetAnimationState(0, 0);
    }

    private void CloneAppear(int k)
    {
        state.Objects.Clones[k]?.Cast(UmadActions.UltimaBlaster);
    }

    private void AttachNumbers()
    {
        for (var k = 0; k < 8; k++)
        {
            if (party.Get(state.Numbers[k]) is not { } member || !member.IsAlive()) continue;
            member.AttachLockonVfx(LockonId.LimitCutNumbers[k]);
        }
    }

    private void QueueCyclonesForWinds(ActionResolved resolved)
    {
        if (resolved.Action.ActionId != ActionId.VacuumWave) return;
        var winds = resolved.Hits.Count(h => h.HasStatus(StatusId.Headwind) || h.HasStatus(StatusId.Tailwind));
        world.Events.Add(4.14f, () => FireCyclones(winds));
    }

    private void FireCyclones(int count)
    {
        windCrystal?.FadeOut();
        windCrystal = null;
        var targets = party.Find.ClosestN(WindCrystalPos, count);
        for (var i = 0; i < targets.Count; i++)
            cycloneHelpers[i % cycloneHelpers.Length]?.Cast(UmadActions.Cyclone, targets[i]);
    }

    private SimCharacter? ChargeTarget(int k)
    {
        if (party.Get(state.Numbers[k]) is { } numbered && numbered.IsAlive()) return numbered;
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
        return alive.Length == 0 ? null : state.PickRandom(alive);
    }

    private void PrepareCharge(int k)
    {
        if (state.Objects.Clones[k] is not { } clone) return;
        var target = ChargeTarget(k);
        var spot = SpotPosition(state.ChargeSpot(k));
        var facing = target != null
            ? MathF.Atan2(target.Position.X - spot.X, target.Position.Z - spot.Z)
            : SpotHeading(state.ChargeSpot(k)) + MathF.PI;
        clone.SetPosition(new Placement(spot, facing));
    }

    private void ResolveCharge(int k)
    {
        state.Objects.Clones[k]?.Cast(UmadActions.UltimaBlasterCharge);
    }

    private static readonly string[] BigBoxGaps = ["A2", "2B", "B3", "3C", "C4", "4D", "D1", "1A"];

    private static string BigBoxGap(float heading)
    {
        var p = OnCircle2(heading, 1f);
        var cwRad = MathF.Atan2(p.X, -p.Y);
        if (cwRad < 0f) cwRad += MathF.Tau;
        var idx = (int)MathF.Floor(cwRad / (MathF.PI / 4f)) % 8;
        return BigBoxGaps[idx];
    }

    private void PrintLimitCutCheatSheet()
    {
        var gap1 = BigBoxGap(state.SafeHeading(0));
        var dirText = !state.Clockwise ? "顺时针" : "逆时针";
        var playerIdx = -1;
        for (var i = 0; i < state.Numbers.Count; i++)
        {
            if (state.Numbers[i] == party.PlayerRole)
            {
                playerIdx = i;
                break;
            }
        }
        var selfHint = playerIdx >= 0
            ? $"（你是 {playerIdx + 1} 号，去 {BigBoxGap(state.SafeHeading(playerIdx))}）"
            : "";
        Plugin.ChatGui?.Print($"[麻将小抄] 1号在 {gap1} 之间，{dirText}{selfHint}");
    }

    private void ScheduleTetherShotCountdown(float shotTime, string label)
    {
        for (var sec = 3; sec >= 1; sec--)
        {
            var remaining = sec;
            AtBh(shotTime - sec, () => Plugin.ChatGui?.Print($"[接线倒数] {label} 判定倒数 {remaining} 秒..."));
        }
    }

    private void ScheduleBlackHolePlayerCallouts()
    {
        if (!state.EnableCheatSheet) return;
        var seat = Array.IndexOf(state.BuildBlackHolePrioritySeats(), party.PlayerRole);
        if (seat < 0) return;

        switch (seat)
        {
            case 4:
                AtBh(21.0f, () => Plugin.ChatGui?.Print("[接线准备] 第1轮第1炮：去接场上唯一的线（攻击1）"));
                ScheduleTetherShotCountdown(32.27f, "第1轮第1炮");
                AtBh(29.5f, () => Plugin.ChatGui?.Print("[接线准备] 第1轮第2炮：去接12点顺时针第1根线（攻击1）"));
                ScheduleTetherShotCountdown(39.33f, "第1轮第2炮");
                AtBh(51.5f, () => Plugin.ChatGui?.Print("[接线准备] 第2轮第1炮：去接12点顺时针第1根线（攻击1，打完交锁链1）"));
                ScheduleTetherShotCountdown(62.83f, "第2轮第1炮");
                break;
            case 0:
                AtBh(28.0f, () => Plugin.ChatGui?.Print("[接线准备] 第1轮第2炮：去接12点顺时针第2根线（攻击2）"));
                ScheduleTetherShotCountdown(39.33f, "第1轮第2炮");
                AtBh(51.5f, () => Plugin.ChatGui?.Print("[接线准备] 第2轮第1~2炮：去接12点顺时针第2根线（攻击2，两炮后交锁链2）"));
                ScheduleTetherShotCountdown(62.83f, "第2轮第1炮");
                ScheduleTetherShotCountdown(67.93f, "第2轮第2炮");
                break;
            case 3:
                AtBh(51.5f, () => Plugin.ChatGui?.Print("[接线准备] 第2轮全程3炮：去接12点顺时针第3根线（攻击3）"));
                ScheduleTetherShotCountdown(62.83f, "第2轮第1炮");
                ScheduleTetherShotCountdown(67.93f, "第2轮第2炮");
                ScheduleTetherShotCountdown(73.03f, "第2轮第3炮");
                break;
            case 5:
                AtBh(59.5f, () => Plugin.ChatGui?.Print("[接力准备] 第2轮第2~3炮：准备从攻击1接手12点顺时针第1根线（锁链1）"));
                ScheduleTetherShotCountdown(67.93f, "第2轮第2炮");
                ScheduleTetherShotCountdown(73.03f, "第2轮第3炮");
                AtBh(85.5f, () => Plugin.ChatGui?.Print("[接线准备] 第3轮第1炮：去接12点顺时针第1根线（锁链1，打完交禁止1）"));
                ScheduleTetherShotCountdown(97.12f, "第3轮第1炮");
                break;
            case 1:
                AtBh(64.5f, () => Plugin.ChatGui?.Print("[接力准备] 第2轮第3炮：准备从攻击2接手12点顺时针第2根线（锁链2）"));
                ScheduleTetherShotCountdown(73.03f, "第2轮第3炮");
                AtBh(85.5f, () => Plugin.ChatGui?.Print("[接线准备] 第3轮第1~2炮：去接12点顺时针第2根线（锁链2，两炮后交禁止2）"));
                ScheduleTetherShotCountdown(97.12f, "第3轮第1炮");
                ScheduleTetherShotCountdown(102.22f, "第3轮第2炮");
                break;
            case 7:
                AtBh(85.5f, () => Plugin.ChatGui?.Print("[接线准备] 第3轮全程3炮：去接12点顺时针第3根线（锁链3）"));
                ScheduleTetherShotCountdown(97.12f, "第3轮第1炮");
                ScheduleTetherShotCountdown(102.22f, "第3轮第2炮");
                ScheduleTetherShotCountdown(107.32f, "第3轮第3炮");
                break;
            case 6:
                AtBh(93.5f, () => Plugin.ChatGui?.Print("[接力准备] 第3轮第2~3炮：准备从锁链1接手12点顺时针第1根线（禁止1）"));
                ScheduleTetherShotCountdown(102.22f, "第3轮第2炮");
                ScheduleTetherShotCountdown(107.32f, "第3轮第3炮");
                AtBh(119.5f, () => Plugin.ChatGui?.Print("[接线准备] 第4轮第1炮：去接12点顺时针第1根线（禁止1）"));
                ScheduleTetherShotCountdown(130.60f, "第4轮第1炮");
                break;
            case 2:
                AtBh(98.5f, () => Plugin.ChatGui?.Print("[接力准备] 第3轮第3炮：准备从锁链2接手12点顺时针第2根线（禁止2）"));
                ScheduleTetherShotCountdown(107.32f, "第3轮第3炮");
                AtBh(119.5f, () => Plugin.ChatGui?.Print("[接线准备] 第4轮第1炮：去接12点顺时针第2根线（禁止2）"));
                ScheduleTetherShotCountdown(130.60f, "第4轮第1炮");
                AtBh(127.5f, () => Plugin.ChatGui?.Print("[接线准备] 第4轮最后1炮：接最后一根线并带到场边分边（禁止2）"));
                ScheduleTetherShotCountdown(137.67f, "第4轮最后1炮");
                break;
        }
    }
}
