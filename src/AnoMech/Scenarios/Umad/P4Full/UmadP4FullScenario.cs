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
using AnoMech.Scenarios.Umad.P4KefkaSays;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P4Full;

public sealed class UmadP4FullScenario : IScenario
{
    public const float PreKefkaSaysOffsetSec = 3.72f;
    public static bool SuppressNextCountdown;

    public string Name => "Full";
    public IPhase Phase => UmadZone.P4;
    public float BgmSecondsAtStart => 0f;
    public float BgmStartDelaySeconds => cd;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;

    private readonly UmadP4FullSettingsWindow settingsWindow = new();
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    public IReadOnlyList<IScenarioAi> AiStrats =>
    [
        new UmadP4FullAi(),
    ];

    private UmadP4FullState state = null!;
    private UmadP4KefkaSaysState ks = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka, neoExdeath, chaos;
    private float cd;

    private static readonly (ushort Id, string Key)[] NeoExdeathTimelines =
    [
        (TimelineId.NeoExdeathShow, "mon_sp/m0418/show/mon_sp001"),
    ];

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(NeoExdeathTimelines, "UmadP4Full");
        instanceWorld.Party.LimitBreak.Set(3f);
    }

    private void AtCombat(float t, Action action) => world.Events.Add(cd + t, action);
    private void AtKs(float t, Action action) => world.Events.Add(cd + PreKefkaSaysOffsetSec + t, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        var suppress = SuppressNextCountdown;
        SuppressNextCountdown = false;
        cd = suppress ? 0f : Math.Clamp(settingsWindow.Overrides.CountdownSeconds, 0f, 15f);

        state = new UmadP4FullState(world, settingsWindow.Overrides, cd);
        ks = state.KefkaSays;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP4FullState>)AiStrats[idx]).Run(state, world);

        ScheduleCountdown();
        ScheduleKefka();
        ScheduleNeoExdeath();
        ScheduleChaos();
        ScheduleKefkaLightning();
        ScheduleKefkaBlizzard();
        ScheduleChaosRaidwides();
        ScheduleAntilights();
        ScheduleChaosMysteries();
        ScheduleNeoExdeathElementsAndShrieks();
        ScheduleInstanceEvents();
        ScheduleDebuffs();
        ScheduleAccelerationBomb();
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

    private void ScheduleKefka()
    {
        world.Events.Add(0f, () => kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Kefka,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: cd <= 0f,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            LayoutId: BNpcLayoutId.KefkaP4,
            Placement: new Placement(Vector3.Zero, 3.140f))));

        if (cd > 0f)
            AtCombat(0f, () => kefka?.SetTargetable(true));

        AtCombat(2.05f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));

        AtKs(1.36f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(1.45f, () => kefka?.Cast(ActionId.KefkaSays, animationLock: 3.1f));
        AtKs(9.41f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(9.59f, () => kefka?.Cast(ActionId.KefkaPoof, animationLock: 1.1f));
        AtKs(10.93f, () => kefka?.AttachLockonVfx(ks.Mystery[0].Blizzard.Lockon));
        AtKs(10.93f, () => kefka?.AttachLockonVfx(ks.Mystery[0].Lightning.Lockon));
        AtKs(11.02f, () => kefka?.Cast(ActionId.MysteryMagic, animationLock: 3.1f));
        AtKs(17.45f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(20.49f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(23.53f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(25.85f, () => kefka?.AttachLockonVfx(ks.Mystery[1].Blizzard.Lockon));
        AtKs(25.85f, () => kefka?.AttachLockonVfx(ks.Mystery[1].Lightning.Lockon));
        AtKs(25.94f, () => kefka?.Cast(ActionId.MysteryMagic, animationLock: 3.1f));
        AtKs(31.57f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(34.61f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(37.64f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(40.68f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(40.99f, () => kefka?.AttachLockonVfx(ks.Mystery[2].Blizzard.Lockon));
        AtKs(40.99f, () => kefka?.AttachLockonVfx(ks.Mystery[2].Lightning.Lockon));
        AtKs(41.08f, () => kefka?.Cast(ActionId.MysteryMagic, animationLock: 3.1f));
        AtKs(48.76f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));

        AtKs(49.25f, () => kefka?.Cast(ActionId.KefkaRest, animationLock: 2.1f));
        AtKs(53.05f, () => kefka?.SetModelState((byte)0x04));
        AtKs(66.10f, () => kefka?.Cast(ActionId.KefkaUnrest, animationLock: 2.1f));
        AtKs(66.91f, () => kefka?.SetModelState((byte)0x00));

        AtKs(68.20f, () => kefka?.Cast(ActionId.ManaCharge, animationLock: 3.1f));
        AtKs(72.00f, () => kefka?.AddStatus(StatusId.ManaCharge));
        AtKs(73.20f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));

        AtKs(74.37f, () => kefka?.AddStatus(StatusId.ThunderCharged));
        AtKs(74.37f, () => kefka?.AttachLockonVfx(ks.Mystery[3].Lightning.Lockon));
        AtKs(74.45f, () => kefka?.Cast(ActionId.ThrummingThunderIII_Cast, animationLock: 3.1f));
        AtKs(81.24f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(84.28f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(84.60f, () => kefka?.Cast(UmadActions.UltimaUpsurge));

        AtKs(92.33f, () => kefka?.AddStatus(StatusId.BlizzardCharged));
        AtKs(92.33f, () => kefka?.AttachLockonVfx(ks.Mystery[3].Blizzard.Lockon));
        AtKs(92.41f, () => kefka?.Cast(ActionId.BlizzardIIIBlowout_Cast, animationLock: 3.1f));
        AtKs(98.13f, () => kefka?.RemoveStatus(StatusId.ManaCharge));

        AtKs(100.41f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(103.45f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(103.54f, () => kefka?.AttachLockonVfx(ks.ManaReleaseBlizzardLockon));
        AtKs(103.54f, () => kefka?.AttachLockonVfx(ks.ManaReleaseLightningLockon));
        AtKs(103.63f, () =>
        {
            kefka?.Cast(ActionId.ManaRelease, animationLock: 3.1f);
            PrintManaReleaseCheatSheet();
        });
        AtKs(111.56f, () => kefka?.RemoveStatus(StatusId.BlizzardCharged));
        AtKs(111.56f, () => kefka?.RemoveStatus(StatusId.ThunderCharged));
        AtKs(113.51f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(116.51f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        AtKs(119.54f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));

        AtCombat(127.58f, () => kefka?.Cast(UmadActions.UltimaUpsurge));
        AtCombat(132.57f, () =>
        {
            kefka?.SetTargetable(false);
            kefka?.Defeat(corpseFadeDelay: 2.0f);
        });
    }

    private void ScheduleNeoExdeath()
    {
        world.Events.Add(0f, () => neoExdeath = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.NeoExdeath,
            NameId: BNpcNameId.NeoExdeath,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.OnlyWhenVisible,
            Visibility: SpawnVisibility.HiddenUntilShown,
            LayoutId: BNpcLayoutId.NeoExdeath,
            Placement: new Placement(new Vector3(20.000f, 0.000f, 0.000f), -1.570f))));

        AtKs(6.32f, () => neoExdeath?.SetPosition(new Placement(new Vector3(14.142f, 0.000f, -14.142f), -0.785f)));
        AtKs(6.46f, () => neoExdeath?.PlayActionTimeline(TimelineId.NeoExdeathShow));

        AtKs(11.28f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: ks.Wave1TrueVal, overrideStacks: true));
        AtKs(11.37f, () => neoExdeath?.Cast(UmadActions.GrandCross));
        AtKs(21.82f, () => neoExdeath?.RemoveStatus(StatusId.KefkaLiesVfx));
        AtKs(26.21f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: ks.Wave2TrueVal, overrideStacks: true));
        AtKs(26.30f, () => neoExdeath?.Cast(UmadActions.GrandCross));
        AtKs(36.81f, () => neoExdeath?.RemoveStatus(StatusId.KefkaLiesVfx));
        AtKs(41.17f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: ks.Wave3TrueVal, overrideStacks: true));
        AtKs(41.26f, () => neoExdeath?.Cast(UmadActions.GrandCross));
        AtKs(51.76f, () => neoExdeath?.RemoveStatus(StatusId.KefkaLiesVfx));

        AtKs(53.28f, () => neoExdeath?.PlayActionTimeline(ActionTimelineId.WarpStart));
        AtKs(55.25f, () => neoExdeath?.SetPosition(ks.NeoExdeathDirection.Apply(new Placement(new Vector3(0, 0, -20), 0))));
        AtKs(55.58f, () => neoExdeath?.PlayActionTimeline(TimelineId.NeoExdeathShow));

        AtKs(57.30f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: ks.Wave4TrueVal, overrideStacks: true));
        AtKs(57.39f, () => neoExdeath?.Cast(ks.Antilights[0].ResolveFloodAction, animationLock: 3.1f));

        AtKs(65.52f, () => neoExdeath?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void ScheduleChaos()
    {
        world.Events.Add(0f, () => chaos = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Chaos,
            NameId: BNpcNameId.Chaos,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.OnlyWhenVisible,
            Visibility: SpawnVisibility.HiddenUntilShown,
            LayoutId: BNpcLayoutId.ChaosP4,
            Placement: new Placement(new Vector3(-18.000f, 0.000f, 0.000f), 1.570f))));

        AtKs(6.37f, () => chaos?.SetPosition(new Placement(new Vector3(-12.728f, 0.000f, -12.728f), 0.785f)));
        AtKs(6.46f, () => chaos?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        AtKs(16.42f, () => chaos?.AddStatus(StatusId.KefkaLiesVfx, stacks: ks.ChaosMysteries[0].StatusValue, overrideStacks: true));
        AtKs(16.51f, () => chaos?.Cast(ks.ChaosMysteries[0].Cast.Action, animationLock: 3.0f));
        AtKs(31.35f, () => chaos?.RemoveStatus(StatusId.KefkaLiesVfx));
        AtKs(31.35f, () => chaos?.AddStatus(StatusId.KefkaLiesVfx, stacks: ks.ChaosMysteries[1].StatusValue, overrideStacks: true));
        AtKs(31.43f, () => chaos?.Cast(ks.ChaosMysteries[1].Cast.Action, animationLock: 3.0f));

        AtKs(43.49f, () => chaos?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void ScheduleKefkaLightning()
    {
        Placement[] placements =
        [
            new(new(24.75f, 0f, -3.54f), -MathF.PI / 4),
            new(new(17.68f, 0f, -10.61f), -MathF.PI / 4),
            new(new(10.61f, 0f, -17.68f), -MathF.PI / 4),
            new(new(3.54f, 0f, -24.75f), -MathF.PI / 4),
        ];
        float[] lightTiming = [11.02f, 25.94f, 41.08f, 74.45f, 110.63f];
        for (var i = 0; i < 4; i++)
        {
            for (var k = 0; k < 5; k++)
            {
                var mystery = ks.Mystery[k];
                var real = (i + mystery.LightningOffset) % 2 == 0;
                var location = placements[i].MulX(mystery.LightningOrientation).MulRot(mystery.LightningOrientation);
                if (real)
                    AtKs(lightTiming[k], () => kefka?.CastFromHelper(mystery.Lightning.Damage, at: location));
                else if (mystery.Lightning.OmenAction != 0)
                    AtKs(lightTiming[k], () => kefka?.CastFromHelper(mystery.Lightning.OmenAction, at: location, animationLock: 1.1f));
            }
        }
    }

    private void ScheduleKefkaBlizzard()
    {
        float[] blizzTiming = [11.02f, 25.94f, 41.08f, 92.41f, 110.63f];
        for (var i = 0; i < 4; i++)
        {
            var from = new Placement(Vector3.Zero, MathF.PI / 2 * i + MathF.PI / 4);
            for (var k = 0; k < 5; k++)
            {
                var mystery = ks.Mystery[k];
                var real = (i + mystery.BlizzardOffset) % 2 == 0;
                if (real)
                    AtKs(blizzTiming[k], () => kefka?.CastFromHelper(mystery.Blizzard.Damage, at: from));
                else if (mystery.Blizzard.OmenAction != 0)
                    AtKs(blizzTiming[k], () => kefka?.CastFromHelper(mystery.Blizzard.OmenAction, at: from, animationLock: 1.1f));
            }
        }
    }

    private void ScheduleChaosRaidwides()
    {
        var center = new Placement(Vector3.Zero, 0f);
        AtKs(16.51f, () => chaos?.CastFromHelper(ks.ChaosMysteries[0].Cast.Raidwide, at: center));
        AtKs(31.43f, () => chaos?.CastFromHelper(ks.ChaosMysteries[1].Cast.Raidwide, at: center));
    }

    private void ScheduleAntilights()
    {
        AtKs(57.39f, () => neoExdeath?.CastFromHelper(UmadActions.EdgeOfDeath, at: ks.NeoExdeathDirection.Apply(new Placement(new Vector3(0, 0, -20), 0))));
        AtKs(57.39f, () => neoExdeath?.CastFromHelper(ks.Antilights[0].Action, at: ks.NeoExdeathDirection.Apply(new Placement(new Vector3(-9.5f, 0f, -20f), 0f))));
        AtKs(57.39f, () => neoExdeath?.CastFromHelper(ks.Antilights[1].Action, at: ks.NeoExdeathDirection.Apply(new Placement(new Vector3(9.5f, 0f, -20f), 0f))));
    }

    private void ScheduleChaosMysteries()
    {
        for (var i = 0; i < 8; i++)
        {
            var role = (PartyRole)i;
            AtKs(87.36f, () => chaos?.CastFromHelper(ks.InfernoMystery.Solution, at: party.Get(role)!.Placement()));
            AtKs(110.07f, () => chaos?.CastFromHelper(ks.TsunamiMystery.Solution, at: party.Get(role)!.Placement()));
        }
    }

    private void ScheduleNeoExdeathElementsAndShrieks()
    {
        for (var i = 0; i < 4; i++)
        {
            var targetId = i * 2 + 1 + (i + 1) % 2;
            var shriekTargetId = i * 4;
            var bolt = i % 2 == 0;
            var first = DeathElement(bolt, stack: bolt ^ ks.ElemTrue[0]);
            var second = DeathElement(bolt, stack: bolt ^ ks.ElemTrue[1]);

            AtKs(71.37f, () => neoExdeath?.CastFromHelper(first, ks.ElemRoles[0].Get(targetId), at: ks.ElemRoles[0].Get(targetId)!.Placement()));
            if (i < 2)
                AtKs(80.49f, () => neoExdeath?.CastFromHelper(DeathShriek(ks.Wave1True), at: new Placement(ks.Wave1.Get(shriekTargetId)!.Position, 0f)));
            AtKs(96.48f, () => neoExdeath?.CastFromHelper(second, ks.ElemRoles[1].Get(targetId), at: new Placement(ks.ElemRoles[1].Get(targetId)!.Position, 0f)));
            if (i < 2)
                AtKs(104.39f, () => neoExdeath?.CastFromHelper(DeathShriek(ks.Wave2True), at: new Placement(ks.Wave2.Get(shriekTargetId)!.Position, 0f)));
        }
    }

    private void ScheduleInstanceEvents()
    {
        AtKs(1.45f, () => world.Map.DirectorUpdate(0x80000027U, 0x1FU, 0x2U, 0x1BDBU, 0x40004142U));
        AtKs(9.50f, () => world.Map.DirectorUpdate(0x80000027U, 0x20U, 0x2U, 0x1BDBU, 0x40004142U));
        AtKs(103.36f, () => world.Map.DirectorUpdate(0x80000027U, 0x4U, 0x2U, 0x1BDBU, 0x40004142U));
        AtKs(119.94f, () => world.Map.DirectorUpdate(0x80000027U, 0x21U, 0x2U, 0x1BDBU, 0x40004142U));
    }

    private void ScheduleDebuffs()
    {
        ushort[] debuffs = [StatusId.AccelerationBomb, StatusId.AccelerationBomb, StatusId.ForkedLightning, StatusId.CompressedWater];
        var d1 = ks.Wave1First ? 51f : 76f;
        float[] durationW1 = [51f, 76f, d1, d1];
        AtKs(20.36f, () =>
        {
            ks.Wave1.ForEach((i, c) => c.AddStatus(debuffs[i % 4], durationW1[i % 4]));
            ks.Wave1.Get(0)?.AddStatus(StatusId.CursedShriek, 60f);
            ks.Wave1.Get(4)?.AddStatus(StatusId.CursedShriek, 60f);
            PrintWaveElemCheatSheet("大十字1", ks.Wave1, ks.Wave1True, durationW1, compact: true);
        });

        AtKs(27.28f, () =>
        {
            party.ForEachActive(c => c.AddStatus(ks.ChaosMysteries[0].Cast.Status, ks.ChaosMysteries[0].Cast.DurationFirst));
            PrintChaosCheatSheet(ks.ChaosMysteries[0]);
        });

        var d2 = ks.Wave1First ? 61f : 36f;
        float[] durationW2 = [61f, 36f, d2, d2];
        AtKs(35.28f, () =>
        {
            ks.Wave2.ForEach((i, c) => c.AddStatus(debuffs[i % 4], durationW2[i % 4]));
            ks.Wave2.Get(0)?.AddStatus(StatusId.CursedShriek, 69f);
            ks.Wave2.Get(4)?.AddStatus(StatusId.CursedShriek, 69f);
            PrintWaveElemCheatSheet("大十字2", ks.Wave2, ks.Wave2True, durationW2, compact: true);
        });

        AtKs(40.99f, () =>
        {
            party.ForEachActive(c => c.AddStatus(ks.ChaosMysteries[1].Cast.Status, ks.ChaosMysteries[1].Cast.DurationSecond));
            PrintChaosCheatSheet(ks.ChaosMysteries[1]);
        });

        ushort[] debuffsW3 = [StatusId.BeyondDeath, StatusId.AllaganField];
        AtKs(51.67f, () =>
        {
            ks.Wave3.ForEach((i, c) => c.AddStatus(debuffsW3[i % 2], 15f));
            ks.Wave3.ForEach((i, c) => c.AddStatus(ks.Wounds[i] ? StatusId.WhiteWound : StatusId.BlackWound));
            PrintWave3CheatSheet();
            PrintRoundCheatSheet(round: 1);
        });
        AtKs(85.50f, () => PrintRoundCheatSheet(round: 2));
        AtKs(66.64f, () => party.ForEachActive(c => c.RemoveStatus(StatusId.WhiteWound)));
        AtKs(66.64f, () => party.ForEachActive(c => c.RemoveStatus(StatusId.BlackWound)));
    }

    private void PrintWaveElemCheatSheet(string tag, RoleList wave, bool isTrue, float[] durations, bool compact = false)
    {
        if (!state.EnableCheatSheet) return;
        var idx = Array.IndexOf(wave.List, party.PlayerRole);
        if (idx < 0) return;
        var slot = idx % 4;
        var isSupport = !party.PlayerRole.IsDps();
        var stackPos = isSupport ? "北A" : "南C";
        var soloPos = isSupport ? "东B" : "西D";
        var tf = isTrue ? "真" : "假";
        var gazeHint = $"{tf}石化({(isTrue ? "人后背对" : "人前面对")})";
        var elem = slot switch { 0 or 1 => "炸弹", 2 => "雷", _ => "水" };
        var msg = compact
            ? $"[{tag}·{tf}{elem}·石化({(isTrue ? "人后背对" : "人前面对")})]"
            : slot switch
        {
            0 => $"[{tag}·{tf}炸弹+{gazeHint}(点你) - {durations[0]:0}s{(isTrue ? "停手" : "移动")}]",
            1 => $"[{tag}·{tf}炸弹 - {durations[1]:0}s{(isTrue ? "停手不动" : "必须移动")} | {gazeHint}]",
            2 => $"[{tag}·{tf}雷 - {durations[2]:0}s{(isTrue ? $"单吃({soloPos})" : $"3人分摊({stackPos})")} | {gazeHint}]",
            _ => $"[{tag}·{tf}水 - {durations[3]:0}s{(isTrue ? $"3人分摊({stackPos})" : $"单吃({soloPos})")} | {gazeHint}]",
        };
        Plugin.ChatGui?.Print(msg);
    }

    private void PrintChaosCheatSheet(ChaosMystery mystery)
    {
        if (!state.EnableCheatSheet) return;
        var tf = mystery.IsTrue ? "真" : "假";
        var elem = mystery.Cast == ChaosCast.Inferno ? "火" : "水";
        var effect = mystery.SolutionIsChariot ? "钢铁" : "月环";
        Plugin.ChatGui?.Print($"[{tf}{elem} - {effect}]");
    }

    private void PrintWave3CheatSheet()
    {
        if (!state.EnableCheatSheet) return;
        var idx = Array.IndexOf(ks.Wave3.List, party.PlayerRole);
        if (idx < 0) return;
        var goLeftFacingBoss = (idx % 2 == 0) ^ ks.Wounds[idx] ^ (ks.Antilights[0].Antilight == Antilight.White);
        Plugin.ChatGui?.Print(goLeftFacingBoss ? "[大十字3 - 面对场外去左]" : "[大十字3 - 面对场外去右]");
    }

    private void PrintRoundCheatSheet(int round)
    {
        if (!state.EnableCheatSheet) return;
        var role = party.PlayerRole;
        var w1 = Array.IndexOf(ks.Wave1.List, role) % 4;
        var w2 = Array.IndexOf(ks.Wave2.List, role) % 4;

        var elemIndex = round - 1;
        var elemSlot = Array.IndexOf(ks.ElemRoles[elemIndex].List, role) % 4;
        var elemTrue = ks.ElemTrue[elemIndex];
        var elemText = elemSlot switch
        {
            2 => elemTrue ? "分散" : "分摊",
            3 => elemTrue ? "分摊" : "分散",
            _ => "分摊",
        };

        string bombText;
        if (round == 1)
        {
            if (w2 == 1) bombText = ks.Wave2True ? "静" : "动";
            else if (w2 != 0 && w1 == 0) bombText = ks.Wave1True ? "静" : "动";
            else bombText = "无动静";
        }
        else
        {
            if (w2 == 0) bombText = ks.Wave2True ? "静" : "动";
            else if (w2 != 1 && w1 == 1) bombText = ks.Wave1True ? "静" : "动";
            else bombText = "无动静";
        }

        var gazeTrue = round == 1 ? ks.Wave1True : ks.Wave2True;
        var selfGaze = round == 1 ? w1 == 0 : w2 == 0;
        var gazeText = (gazeTrue ? "背对石化" : "面对石化") + (selfGaze ? "(点你)" : "");

        var chaosMystery = round == 1 ? ks.InfernoMystery : ks.TsunamiMystery;
        var shapeText = chaosMystery.SolutionIsChariot ? "钢铁" : "月环";

        var tag = round == 1 ? "P4第一轮" : "P4第二轮";
        Plugin.ChatGui?.Print($"[{tag}] {elemText}+{bombText} -> {gazeText} -> {shapeText}");
    }

    private void PrintManaReleaseCheatSheet()
    {
        if (!state.EnableCheatSheet) return;
        var m = ks.Mystery[4];
        var coneSafe = m.Blizzard == KefkaCast.BlizzardFake;
        var lineSafe = m.Lightning == KefkaCast.LightningFake;
        var safeZone = (coneSafe, lineSafe) switch
        {
            (true, true) => "扇形+直条",
            (true, false) => "仅扇形",
            (false, true) => "仅直条",
            _ => "空白区(扇形/直条都躲)",
        };
        Plugin.ChatGui?.Print($"[魔法放出] 安全区：{safeZone}");
    }

    private void ScheduleAccelerationBomb()
    {
        var w1 = Array.IndexOf(ks.Wave1.List, party.PlayerRole) % 4;
        var w2 = Array.IndexOf(ks.Wave2.List, party.PlayerRole) % 4;

        if (w2 == 0) AtKs(96.28f, () => ResolveAccelerationBomb(ks.Wave2True));
        else if (w2 == 1) AtKs(71.28f, () => ResolveAccelerationBomb(ks.Wave2True));
        else if (w1 == 0) AtKs(71.36f, () => ResolveAccelerationBomb(ks.Wave1True));
        else if (w1 == 1) AtKs(96.36f, () => ResolveAccelerationBomb(ks.Wave1True));
    }

    private void ResolveAccelerationBomb(bool real)
    {
        var player = party.Player;
        if (player == null || !player.IsAlive()) return;
        if (real ? player.IsActing : !player.IsActing)
            neoExdeath?.CastFromHelper(UmadActions.DeathBomb, player);
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (lost.StatusId == ks.BeyondDeathStatus && lost.Reason == StatusLossReason.Expired)
            lost.Holder.DieOfStatus(lost.StatusId, "not cleansed");
        else if (lost.StatusId == ks.AllaganFieldStatus && lost.Reason != StatusLossReason.Removed)
            DetonateAllaganField(lost.Holder, lost.Reason == StatusLossReason.Died ? UmadActions.DeathSurgeWipe : UmadActions.DeathSurge);
    }

    private void DetonateAllaganField(SimCharacter holder, EnemyAction surge)
    {
        if (holder is not ISimPartyMember member || !ks.Wave3.List.Contains(member.Role)) return;
        var at = new Placement(holder.Position, 0f);
        world.Events.Add(0.1f, () => neoExdeath?.CastFromHelper(surge, at: at));
    }

    private static EnemyAction DeathElement(bool bolt, bool stack) => (bolt, stack) switch
    {
        (true, true) => UmadActions.DeathBoltStack,
        (true, false) => UmadActions.DeathBoltSpread,
        (false, true) => UmadActions.DeathWaveStack,
        (false, false) => UmadActions.DeathWaveSpread,
    };

    private static EnemyAction DeathShriek(bool real) => real ? UmadActions.DeathShriekLookAway : UmadActions.DeathShriekLookAt;

    public void Tick(float delta, float elapsed)
    {
    }
}
