using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullScenario : IScenario
{
    public const float PreTargetableDurationSec = 29.99f;
    internal const float FloodBaseSec = 18.64f;
    internal const float Orchestra1BaseSec = 32.83f;
    internal const float CelestriadBaseSec = 53.55f;
    internal const float ExaflaresBaseSec = 96.55f;
    internal const float Orchestra2BaseSec = 124.90f;
    internal const float ForsakenBaseSec = 149.62f;
    public static bool SuppressNextCountdown;

    public string Name => "Full";
    public IPhase Phase => UmadZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;

    private readonly UmadP5FullSettingsWindow settingsWindow = new();
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    public IReadOnlyList<IScenarioAi> AiStrats =>
    [
        new UmadP5FullAi(),
    ];

    private UmadP5FullState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka;
    private readonly List<SimEventObject?> celestriadTowers = [];
    private readonly List<Vector2> activeTrapCenters = [];
    private readonly float[] trapCooldown = new float[8];
    private float cd;
    private float preTargetable;
    private bool solo;
    private bool p5ClearedNaturally;

    private const float FloodCastStart = 1.0f;
    private const float StackLeadBeforeWaves = 0.12f;
    private const float HelperAnimationLock = 1.1f;
    private const float FloodCastAnimationLock = 3.1f;

    private const float ExaflareFirstHitDelay = 4.582f;
    private const float ExaflareHitInterval = 0.513f;
    private const int ExaflareHitCount = 6;
    private const float ExaflareSpreadT = 25.09f;
    private const float KefkaAnimationLock = 3.1f;

    private const float ForsakenTrapRadius = 6.5f;
    private const float ForsakenTrapRadiusSq = ForsakenTrapRadius * ForsakenTrapRadius;

    private const float Along = 20f;
    private const float NearOffset = 5f;
    private const float FarOffset = 15f;
    private const float NeSwRotation = MathF.PI / 4f;
    private const float NwSeRotation = -MathF.PI / 4f;
    private static readonly float InvSqrt2 = 1f / MathF.Sqrt(2f);

    private static Vector3 NeSwPoint(float perp) => new((-Along + perp) * InvSqrt2, 0f, (-Along - perp) * InvSqrt2);
    private static Vector3 NwSePoint(float perp) => new(-(-Along + perp) * InvSqrt2, 0f, (-Along - perp) * InvSqrt2);

    private static Vector3 LaneMiddle(Vector3 anchor, float rotation)
        => anchor + new Vector3(MathF.Sin(rotation), 0f, MathF.Cos(rotation)) * Along;

    private static readonly Vector3[] NeSwMarch =
    [
        NeSwPoint(NearOffset),
        NeSwPoint(FarOffset),
        NeSwPoint(-FarOffset),
        NeSwPoint(-NearOffset),
    ];

    private static readonly Vector3[] NwSeMarch =
    [
        NwSePoint(NearOffset),
        NwSePoint(FarOffset),
        NwSePoint(-FarOffset),
        NwSePoint(-NearOffset),
    ];

    private static readonly (ushort Id, string Key)[] WaveTimelines =
    [
        (10690, "mon_sp/gimmick/z5r2_boss2_gimmick02"),
        (10692, "mon_sp/gimmick/z5r2_boss2_gimmick04"),
        (1378, "mon_sp/gimmick/monster_hanyou_hitclip_nomi_saisoku"),
    ];

    private static readonly PartyRole[] TankRoles = [PartyRole.MainTank, PartyRole.OffTank];
    private static readonly PartyRole[] HealerRoles = [PartyRole.RegenHealer, PartyRole.ShieldHealer];
    private static readonly PartyRole[] DpsRoles =
    [
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    private readonly record struct ForsakenWaveSpec(
        float WaveTime,
        uint? DirectorParam,
        Vector3 QuadrantPos,
        Vector3 Ground1Pos,
        byte Ground1Index,
        Vector3 Ground2Pos,
        byte Ground2Index,
        byte WaveVisualIndex);

    private static readonly ForsakenWaveSpec[] ForsakenWaves =
    [
        new(159.61f, null,    new(-7.00f, 0f,  6.75f), new(-13.50f, 0f,  13.50f), 0x1C, new(  0.00f, 0f,   0.00f), 0x14, 0x1D),
        new(167.81f, 0x29U,   new(-6.75f, 0f, -6.75f), new( 13.50f, 0f, -13.50f), 0x1A, new( 13.50f, 0f,  13.50f), 0x19, 0x1E),
        new(175.96f, 0x2AU,   new( 7.00f, 0f, -6.75f), new( 13.50f, 0f,   0.00f), 0x15, new(-13.50f, 0f, -13.50f), 0x1B, 0x1F),
        new(184.11f, 0x2BU,   new( 6.75f, 0f,  6.75f), new(  0.00f, 0f,  13.50f), 0x18, new(  0.00f, 0f, -13.50f), 0x16, 0x20),
    ];

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(WaveTimelines, "UmadP5Full");
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        Towers.ResolveTower(world, resolved, ActionId.CelestriadFireIII, UmadActions.StardustFireIII, minSoakers: 1);
        Towers.ResolveTower(world, resolved, ActionId.CelestriadBlizzardIII, UmadActions.StardustBlizzardIII, minSoakers: 1);
        Towers.ResolveTower(world, resolved, ActionId.CelestriadThunderIII, UmadActions.StardustThunderIII, minSoakers: 1);
    }

    private void AtIntro(float t, Action action) => world.Events.Add(cd + t, action);
    private void AtCombat(float t, Action action) => world.Events.Add(cd + preTargetable + t, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        solo = !selectedAi.HasValue;
        celestriadTowers.Clear();
        activeTrapCenters.Clear();
        Array.Clear(trapCooldown);
        p5ClearedNaturally = false;
        if (Plugin.Game is { } game)
        {
            game.RunFinished -= OnP5RunFinished;
            game.RunFinished += OnP5RunFinished;
        }

        var isChained = SuppressNextCountdown;
        SuppressNextCountdown = false;
        cd = isChained ? 0f : Math.Clamp(settingsWindow.Overrides.CountdownSeconds, 0f, 15f);
        preTargetable = (isChained || settingsWindow.Overrides.PlayOpeningTransitionWhenSolo)
            ? PreTargetableDurationSec
            : 0f;

        state = new UmadP5FullState(world, settingsWindow.Overrides, cd, preTargetable);

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5FullState>)AiStrats[idx]).Run(state, world);

        ScheduleCountdown();
        ScheduleTransitionAndKefka();
        ScheduleChainUltimaAndRoleStacks(3.12f, roleStackWaves: 3);
        ScheduleFlood();
        ScheduleMaddeningOrchestra(
            Orchestra1BaseSec,
            state.Orchestra1Wave1Holy,
            state.Orchestra1FlareTank,
            state.Orchestra1HolyTank,
            roleStackWaves: 2);
        ScheduleCelestriad();
        ScheduleChainUltimaAndRoleStacks(85.03f, roleStackWaves: 2);
        ScheduleExaflares();
        ScheduleMaddeningOrchestra(
            Orchestra2BaseSec,
            state.Orchestra2Wave1Holy,
            state.Orchestra2FlareTank,
            state.Orchestra2HolyTank,
            roleStackWaves: 3);
        ScheduleForsakenNull();
    }

    private void ScheduleCountdown()
    {
        if (cd <= 0f) return;
        var secs = (int)MathF.Ceiling(cd);
        for (var s = secs; s >= 1; s--)
        {
            var remaining = s;
            world.Events.Add(MathF.Max(0f, cd - s), () => Plugin.ChatGui?.Print($"开怪倒数 {remaining} 秒..."));
        }
    }

    private void ScheduleTransitionAndKefka()
    {
        var immediateTargetable = cd <= 0f && preTargetable <= 0f;
        world.Events.Add(0f, () => kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP5,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: immediateTargetable,
            EnemyList: EnemyListMode.Always,
            Visibility: preTargetable > 0f ? SpawnVisibility.HiddenUntilShown : SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, -MathF.PI),
            LayoutId: BNpcLayoutId.KefkaP5)));

        if (preTargetable > 0f)
        {
            AtIntro(0.96f, () => world.Map.DirectorUpdate(0x80000028U, 0x00U, 0x04U, 0x06U, 0U));
            AtIntro(3.10f, () => world.Map.DirectorUpdate(0x80000027U, 0x22U, 0x02U, 0x1BDBU, 0U));
            AtIntro(9.70f, () => world.Map.DirectorUpdate(0x80000001U, 0x4F46U, 0x00U, 0x00U, 0U));
            AtIntro(15.82f, () => world.Map.DirectorUpdate(0x80000027U, 0x23U, 0x02U, 0x1BDBU, 0U));
            AtIntro(22.95f, () =>
            {
                world.Map.DirectorUpdate(0x80000027U, 0x24U, 0x02U, 0x1BDBU, 0U);
                kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            });
            AtIntro(29.96f, () => world.Map.DirectorUpdate(0x8000000DU, 0x02U, 0x00U, 0x00U, 0U));
        }

        if (!immediateTargetable)
            AtCombat(0f, () => kefka?.SetTargetable(true));
    }

    private void ScheduleChainUltimaAndRoleStacks(float castStartSec, int roleStackWaves)
    {
        AtCombat(castStartSec, () => kefka?.Cast(ActionId.ChainUltimaCast, animationLock: 3.1f));

        float[] chainHitOffsets = [5.08f, 5.79f, 6.46f, 7.17f];
        foreach (var offset in chainHitOffsets)
            AtCombat(castStartSec + offset, () => kefka?.CastFromHelper(UmadActions.ChainUltimaHit, at: new Placement(Vector3.Zero, -MathF.PI)));

        float[] roleStackCastOffsets = [9.13f, 12.29f, 15.41f];
        for (var i = 0; i < roleStackWaves && i < roleStackCastOffsets.Length; i++)
        {
            var tCast = castStartSec + roleStackCastOffsets[i];
            AtCombat(tCast, () => kefka?.Cast(ActionId.MagicStrikeCast, animationLock: 1.1f));
            AtCombat(tCast + 0.80f, ResolveRoleStacks);
        }
    }

    private void ResolveRoleStacks()
    {
        if (solo) return;
        CastRoleStack(TankRoles, UmadActions.MagicStrikeTank);
        CastRoleStack(HealerRoles, UmadActions.MagicStrikeHealer);
        CastRoleStack(DpsRoles, UmadActions.MagicStrikeDps);
    }

    private void CastRoleStack(PartyRole[] roles, EnemyAction action)
    {
        var living = new List<SimCharacter>(roles.Length);
        foreach (var role in roles)
        {
            if (party.Get(role) is { } member && member.IsAlive())
                living.Add(member);
        }
        if (living.Count == 0) return;
        var target = living[world.Rng.Next(living.Count)];
        kefka?.CastFromHelper(action, target, at: new Placement(Vector3.Zero, -MathF.PI));
    }

    private void ScheduleFlood()
    {
        AtCombat(FloodBaseSec + FloodCastStart, () => kefka?.Cast(ActionId.FloodCast, animationLock: FloodCastAnimationLock));

        var neSw = MarchPairs(NeSwMarch, state.Flood.NeSwReversed);
        var nwSe = MarchPairs(NwSeMarch, state.Flood.NwSeReversed);
        var (first, second, firstRot, secondRot) = state.Flood.NeSwFirst
            ? (neSw, nwSe, NeSwRotation, NwSeRotation)
            : (nwSe, neSw, NwSeRotation, NeSwRotation);
        Span<(Vector3 a, Vector3 b, float rotation)> ticks =
        [
            (first.pair1.Item1, first.pair1.Item2, firstRot),
            (second.pair1.Item1, second.pair1.Item2, secondRot),
            (first.pair2.Item1, first.pair2.Item2, firstRot),
            (second.pair2.Item1, second.pair2.Item2, secondRot),
        ];

        for (var tick = 0; tick < UmadP5FloodScenario.TickCount; tick++)
        {
            var tTelegraph = FloodBaseSec + UmadP5FloodScenario.FirstTelegraphAt + tick * UmadP5FloodScenario.TelegraphStagger;
            var tResolve = tTelegraph + UmadP5FloodScenario.ResolveDelayAfterTelegraph;
            var (a, b, rotation) = ticks[tick];
            AtCombat(tTelegraph, () => TelegraphFloodTick(a, b, rotation));
            if (!solo) AtCombat(tResolve - StackLeadBeforeWaves, ResolveChaoticFlood);
            AtCombat(tResolve, () => ResolveFloodWaves(a, b, rotation));
        }
    }

    private void ScheduleMaddeningOrchestra(
        float baseSec,
        IReadOnlyList<PartyRole> wave1Holy,
        PartyRole flareTank,
        PartyRole holyTank,
        int roleStackWaves)
    {
        AtCombat(baseSec, () => kefka?.Cast(ActionId.MaddeningOrchestraCast, animationLock: 3.1f));

        AtCombat(baseSec + 5.88f, () =>
        {
            CastOnRole(PartyRole.MainTank, UmadActions.OrchestraFlare);
            CastOnRole(PartyRole.OffTank, UmadActions.OrchestraFlare);
            foreach (var role in wave1Holy)
                CastOnRole(role, UmadActions.OrchestraHoly);
        });

        AtCombat(baseSec + 8.16f, () => kefka?.Cast(ActionId.MaddeningOrchestraWave2, animationLock: 1.1f));

        AtCombat(baseSec + 9.05f, () =>
        {
            if (!solo) CastRoleStack(TankRoles, UmadActions.ChaoticFlare);
            var closestNonTanks = HealerRoles.Concat(DpsRoles)
                .Select(r => party.Get(r))
                .Where(c => c is not null && c.IsAlive())
                .OrderBy(c => c!.Position.X * c.Position.X + c.Position.Z * c.Position.Z)
                .Take(3);
            foreach (var target in closestNonTanks)
                kefka?.CastFromHelper(UmadActions.OrchestraHoly, target!, at: new Placement(Vector3.Zero, -MathF.PI));
        });

        AtCombat(baseSec + 12.52f, () =>
        {
            CastOnRole(flareTank, UmadActions.FlareSpread);
            CastOnRole(holyTank, UmadActions.ChaoticHoly);
        });

        float[] castOffsets = [16.36f, 19.48f, 22.59f];
        for (var i = 0; i < roleStackWaves && i < castOffsets.Length; i++)
        {
            var tCast = baseSec + castOffsets[i];
            AtCombat(tCast, () => kefka?.Cast(ActionId.MagicStrikeCast, animationLock: 1.1f));
            AtCombat(tCast + 0.80f, ResolveRoleStacks);
        }
    }

    private void CastOnRole(PartyRole role, EnemyAction action)
    {
        if (party.Get(role) is { } target && target.IsAlive())
            kefka?.CastFromHelper(action, target, at: new Placement(Vector3.Zero, -MathF.PI));
    }

    private void ScheduleCelestriad()
    {
        AtCombat(CelestriadBaseSec + 1.0f, () => kefka?.Cast(ActionId.Celestriad, animationLock: 3.1f));
        AtCombat(CelestriadBaseSec + 6.1f, ApplyCelestriadDebuffs);
        AtCombat(CelestriadBaseSec + 6.22f, SpawnCelestriadTowers);
        AtCombat(CelestriadBaseSec + 9.05f, () => LightCelestriadTowers(0));
        AtCombat(CelestriadBaseSec + 10.17f, () => LaunchCelestriadChoice(0));
        AtCombat(CelestriadBaseSec + 15.06f, () => SpendCelestriadTowers(0));
        AtCombat(CelestriadBaseSec + 15.06f, () => ResolveCelestriadTowers(0));
        AtCombat(CelestriadBaseSec + 15.06f, () => LightCelestriadTowers(1));
        AtCombat(CelestriadBaseSec + 15.26f, () => ResolveCelestriadChoice(0));
        AtCombat(CelestriadBaseSec + 21.01f, () => SpendCelestriadTowers(1));
        AtCombat(CelestriadBaseSec + 21.01f, () => LightCelestriadTowers(2));
        AtCombat(CelestriadBaseSec + 21.11f, () => ResolveCelestriadTowers(1));
        AtCombat(CelestriadBaseSec + 22.30f, () => LaunchCelestriadChoice(2));
        AtCombat(CelestriadBaseSec + 27.10f, () => SpendCelestriadTowers(2));
        AtCombat(CelestriadBaseSec + 27.10f, () => ResolveCelestriadTowers(2));
        AtCombat(CelestriadBaseSec + 27.39f, () => ResolveCelestriadChoice(2));
        AtCombat(CelestriadBaseSec + 29.44f, DespawnCelestriadTowers);
    }

    private void ScheduleExaflares()
    {
        AtCombat(ExaflaresBaseSec + 2.89f, () =>
        {
            if (kefka is { } boss)
                world.Map.DirectorUpdate(0x80000027U, 0x25U, 0x2U, 0x1BDBU, boss.EntityId);
        });
        AtCombat(ExaflaresBaseSec + 3.0f, () => kefka?.Cast(ActionId.ChaosEnd1, animationLock: KefkaAnimationLock));

        var rollingHits = new List<(float T, Vector3 Position)>();
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 3.0f, state.Exaflares.LeftOrder[0], isLeft: true));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 3.0f, state.Exaflares.LeftOrder[1], isLeft: true));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 5.5f, state.Exaflares.RightOrder[0], isLeft: false));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 5.5f, state.Exaflares.RightOrder[1], isLeft: false));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 8.0f, state.Exaflares.LeftOrder[2], isLeft: true));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 8.0f, state.Exaflares.LeftOrder[3], isLeft: true));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 10.5f, state.Exaflares.RightOrder[2], isLeft: false));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 10.5f, state.Exaflares.RightOrder[3], isLeft: false));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 13.0f, state.Exaflares.LeftOrder[4], isLeft: true));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 13.0f, state.Exaflares.LeftOrder[5], isLeft: true));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 15.5f, state.Exaflares.RightOrder[4], isLeft: false));
        rollingHits.AddRange(LaunchExaflareLine(ExaflaresBaseSec + 15.5f, state.Exaflares.RightOrder[5], isLeft: false));

        AtCombat(ExaflaresBaseSec + 19.10f, () => kefka?.VoiceLine(8206500));
        AtCombat(ExaflaresBaseSec + 19.2f, () => kefka?.Cast(ActionId.ChaosEnd2, animationLock: KefkaAnimationLock));
        var spreadSpots = LastRollingHitSpots(rollingHits, 8);
        AtCombat(ExaflaresBaseSec + ExaflareSpreadT, () => ResolveExaflareSpread(spreadSpots));
    }

    private void ScheduleForsakenNull()
    {
        AtCombat(ForsakenBaseSec - 0.09f, () =>
        {
            if (kefka is { } boss)
                world.Map.DirectorUpdate(0x80000027U, 0x28U, 0x2U, 0x1BDBU, boss.EntityId);
        });
        AtCombat(ForsakenBaseSec, () => kefka?.Cast(UmadActions.ForsakenP5Cast));
        AtCombat(159.61f, () => kefka?.AddStatus(StatusId.DirectionalDisregard));

        for (var i = 0; i < ForsakenWaves.Length; i++)
        {
            var wave = ForsakenWaves[i];
            var stackRole = state.ForsakenStackTargets[i];

            if (wave.DirectorParam is { } dirParam)
            {
                AtCombat(wave.WaveTime - 0.09f, () =>
                {
                    if (kefka is { } boss)
                        world.Map.DirectorUpdate(0x80000027U, dirParam, 0x2U, 0x1BDBU, boss.EntityId);
                });
                AtCombat(wave.WaveTime, () => kefka?.Cast(UmadActions.ForsakenP5Pulse));
            }

            AtCombat(wave.WaveTime, () =>
            {
                var target = ResolveLivingRoleOrPlayer(stackRole);
                target?.AttachLockonVfx(LockonId.Stack);
            });

            AtCombat(wave.WaveTime + 0.08f, () =>
            {
                var baitPos = ResolveLivingRoleOrPlayer(stackRole)?.Position ?? wave.QuadrantPos;
                kefka?.CastFromHelper(UmadActions.ForsakenQuadrant, at: new Placement(new Vector3(baitPos.X, 0f, baitPos.Z), 0f));
                kefka?.CastFromHelper(UmadActions.ForsakenGround, at: new Placement(wave.Ground1Pos, 0f));
                kefka?.CastFromHelper(UmadActions.ForsakenGround, at: new Placement(wave.Ground2Pos, 0f));
            });

            AtCombat(wave.WaveTime + 2.00f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: wave.WaveVisualIndex));

            if (!solo)
            {
                AtCombat(wave.WaveTime + 5.10f, () =>
                {
                    var target = ResolveLivingRoleOrPlayer(stackRole);
                    if (target is not null)
                        kefka?.CastFromHelper(UmadActions.ForsakenBonds, target, at: new Placement(Vector3.Zero, -MathF.PI));
                });
            }

            AtCombat(wave.WaveTime + 5.65f, () =>
            {
                world.Map.AddEffect(packetFlags: 0x00200010U, index: wave.Ground1Index);
                world.Map.AddEffect(packetFlags: 0x00200010U, index: wave.Ground2Index);
                activeTrapCenters.Add(new Vector2(wave.Ground1Pos.X, wave.Ground1Pos.Z));
                activeTrapCenters.Add(new Vector2(wave.Ground2Pos.X, wave.Ground2Pos.Z));
            });
        }

        AtCombat(194.26f, () => kefka?.Cast(UmadActions.ForsakenNullEnrage));
        AtCombat(195.02f, () =>
        {
            world.Map.AddEffect(packetFlags: 0x00100008U, index: 0x21);
            world.Map.AddEffect(packetFlags: 0x00080004U, index: 0x1D);
            world.Map.AddEffect(packetFlags: 0x00080004U, index: 0x1E);
            world.Map.AddEffect(packetFlags: 0x00080004U, index: 0x1F);
            world.Map.AddEffect(packetFlags: 0x00080004U, index: 0x20);
        });
        AtCombat(198.53f, () =>
        {
            if (kefka is { } boss)
                world.Map.DirectorUpdate(0x80000027U, 0x2CU, 0x2U, 0x1BDBU, boss.EntityId);
        });

        if (state.ClearTimeSec < 219.5f)
        {
            AtCombat(state.ClearTimeSec, () =>
            {
                p5ClearedNaturally = true;
                activeTrapCenters.Clear();
                kefka?.SetTargetable(false);
                kefka?.Defeat(corpseFadeDelay: 2.0f);
            });
        }
    }


    private void OnP5RunFinished(bool clean)
    {
        if (Plugin.Game is { } game)
            game.RunFinished -= OnP5RunFinished;

        if (!clean || !p5ClearedNaturally || !world.Map.IsInInstance) return;

        activeTrapCenters.Clear();
        for (byte idx = 0x14; idx <= 0x21; idx++)
            world.Map.AddEffect(packetFlags: 0x00080004U, index: idx);
        world.Map.AddEffect(packetFlags: 0x00080004U, index: 0x00);
        world.Map.DirectorUpdate(0x80000028U, 0x04U, 0U, 0U, 0U);
        world.Map.DirectorUpdate(0x8000000DU, 0x01U, 0U, 0U, 0U);
        world.Map.SetWeather(77, transition: 1.5f);
    }

    private SimCharacter? ResolveLivingRoleOrPlayer(PartyRole preferred)
    {
        if (party.Get(preferred) is { } target && target.IsAlive()) return target;
        if (party.Player is { } player && player.IsAlive()) return player;
        for (var i = 0; i < 8; i++)
        {
            if (party.Get(i) is { } member && member.IsAlive()) return member;
        }
        return null;
    }

    private IEnumerable<(float T, Vector3 Position)> LaunchExaflareLine(float startCombatT, int lineIdx, bool isLeft)
    {
        var initPos = isLeft
            ? new Vector3(-35f + 5f * lineIdx, 0f, -5f * lineIdx)
            : new Vector3(5f * lineIdx, 0f, -35f + 5f * lineIdx);
        var dPos = isLeft ? new Vector3(5f, 0f, 5f) : new Vector3(-5f, 0f, 5f);
        var heading = MathF.PI * (isLeft ? 1f : -1f) / 4f;

        AtCombat(startCombatT, () =>
            kefka?.CastFromHelper(ActionId.ExaflareOmen, at: new Placement(initPos, heading), animationLock: HelperAnimationLock));

        var hits = new List<(float T, Vector3 Position)>();
        for (var i = 0; i < ExaflareHitCount; i++)
        {
            var pos = initPos + dPos * (i + 1);
            var tErupt = startCombatT + ExaflareFirstHitDelay + ExaflareHitInterval * i;
            hits.Add((tErupt, pos));
            AtCombat(tErupt, () => kefka?.CastFromHelper(UmadActions.ExaflareHit, at: new Placement(pos, 0f)));
        }
        return hits;
    }

    private void ResolveExaflareSpread(Vector3[] spreadSpots)
    {
        for (var i = 0; i < 8; i++)
        {
            var member = party.Get(i);
            if (member is null || !member.IsAlive()) continue;
            kefka?.CastFromHelper(UmadActions.ExaflareSpread, member, at: new Placement(spreadSpots[i], 0f));
        }
    }

    private static Vector3[] LastRollingHitSpots(IEnumerable<(float T, Vector3 Position)> rollingHits, int count) =>
        rollingHits.OrderByDescending(h => h.T).Select(h => h.Position).Distinct().Take(count).ToArray();

    private void ApplyCelestriadDebuffs()
    {
        foreach (var (role, element) in state.Celestriad.PlayerDebuffElement)
        {
            if (element is not { } e) continue;
            party.Get(role)?.AddStatus(e.VulnUpStatusId, 20f);
        }
    }

    private void SpawnCelestriadTowers()
    {
        foreach (var tower in state.Celestriad.AllTowers)
        {
            celestriadTowers.Add(world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = tower.Element.TowerEObjId,
                LayoutId = EObjId.CelestriadTowerLayoutIds[tower.RingIndex],
                EventId = EObjId.PropEventId,
                TargetableStatus = 5,
                Arg2 = EObjId.CelestriadTowerArg2 + ((uint)tower.RingIndex << 16),
                Placement = new Placement(tower.Position, MathF.Atan2(-tower.Position.X, -tower.Position.Z)),
                TimelineState = EObjState.CelestriadTowerDormant,
            }));
        }
    }

    private void LightCelestriadTowers(int set)
    {
        foreach (var towerIndex in state.Celestriad.SetActiveTowers[set])
            celestriadTowers[towerIndex]?.PlayBeat(EObjState.CelestriadTowerLit, EObjState.CelestriadTowerLitMask);
    }

    private void SpendCelestriadTowers(int set)
    {
        foreach (var towerIndex in state.Celestriad.SetActiveTowers[set])
            celestriadTowers[towerIndex]?.PlayBeat(EObjState.CelestriadTowerSpent, EObjState.CelestriadTowerSpentMask);
    }

    private void ResolveCelestriadTowers(int set)
    {
        foreach (var towerIndex in state.Celestriad.SetActiveTowers[set])
        {
            var tower = state.Celestriad.AllTowers[towerIndex];
            kefka?.CastFromHelper(tower.Element.Tower, at: new Placement(tower.Position, 0f));
        }
    }

    private void LaunchCelestriadChoice(int set)
    {
        if (state.Celestriad.AeroVariant[set] is not { } choice || kefka is null) return;
        kefka.Cast(choice.CastActionId, animationLock: 3.8f);
    }

    private void ResolveCelestriadChoice(int set)
    {
        if (state.Celestriad.AeroVariant[set] is { } choice)
            kefka?.CastFromHelper(choice.Resolution, at: new Placement(Vector3.Zero, MathF.PI));
    }

    private void DespawnCelestriadTowers()
    {
        foreach (var tower in celestriadTowers) tower?.Despawn();
        celestriadTowers.Clear();
    }

    private static ((Vector3, Vector3) pair1, (Vector3, Vector3) pair2) MarchPairs(Vector3[] points, bool reversed)
    {
        var p = reversed ? [points[3], points[2], points[1], points[0]] : points;
        return ((p[0], p[2]), (p[1], p[3]));
    }

    private void TelegraphFloodTick(Vector3 a, Vector3 b, float rotation)
    {
        kefka?.CastFromHelper(ActionId.FloodTelegraph, a, at: new Placement(LaneMiddle(a, rotation), rotation), animationLock: HelperAnimationLock);
        kefka?.CastFromHelper(ActionId.FloodTelegraph, b, at: new Placement(LaneMiddle(b, rotation), rotation), animationLock: HelperAnimationLock);
    }

    private void ResolveFloodWaves(Vector3 a, Vector3 b, float rotation)
    {
        kefka?.CastFromHelper(UmadActions.FloodWave, a, at: new Placement(LaneMiddle(a, rotation), rotation));
        kefka?.CastFromHelper(UmadActions.FloodWave, b, at: new Placement(LaneMiddle(b, rotation), rotation));
    }

    private void ResolveChaoticFlood()
    {
        SimCharacter? target = null;
        var role = state.Flood.PickStackTarget();
        for (var attempt = 0; attempt < 8 && target is null; attempt++)
        {
            var candidate = party.Get(role);
            if (candidate is { } c && c.IsAlive()) target = c;
            else role = state.Flood.PickStackTarget();
        }
        if (target is null) return;
        kefka?.CastFromHelper(UmadActions.ChaoticFlood, target, at: new Placement(Vector3.Zero, MathF.PI));
    }

    public void Tick(float delta, float elapsed)
    {
        state?.Exaflares.SpreadTick?.Invoke(delta);

        if (activeTrapCenters.Count == 0 || kefka is null) return;

        for (var slot = 0; slot < 8; slot++)
        {
            if (trapCooldown[slot] > 0f)
            {
                trapCooldown[slot] -= delta;
                continue;
            }
            var member = party.Get(slot);
            if (member is null || !member.IsAlive()) continue;
            var pos = new Vector2(member.Position.X, member.Position.Z);
            for (var i = 0; i < activeTrapCenters.Count; i++)
            {
                if (Vector2.DistanceSquared(pos, activeTrapCenters[i]) < ForsakenTrapRadiusSq)
                {
                    trapCooldown[slot] = 1.5f;
                    var c = activeTrapCenters[i];
                    kefka.CastFromHelper(UmadActions.ForsakenTrap, member, at: new Placement(new Vector3(c.X, 0f, c.Y), 0f));
                    break;
                }
            }
        }
    }
}
