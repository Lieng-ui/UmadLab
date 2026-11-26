using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.FullEncounter;

/// <summary>
/// 绝妖星 P5 魔导神凯夫卡完整时间轴场景（899.13s ~ 1116.38s，总长 217.25s）：
/// - 刷出神凯夫卡（BNpcBaseId.KefkaP5 = 19511 + BNpcLayoutId.KefkaP5）
/// - 全程可选中直至狂暴读条遗弃末点（194.26f）并在 217.25s（1116.38s）过本收尾
/// </summary>
public sealed class UmadP5FullScenario : IScenario
{
    public string Name => "Umad P5 Full (MinLoop)";
    public IPhase Phase => UmadZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => Array.Empty<IScenarioAi>();
    public float BgmSecondsAtStart => 899.13f;

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
    }

    // A real cast: the sheet's area is used for the hit test, and the (zero-damage) Damage
    // effect is what makes EnemyActionHandler schedule Resolve at all — a cast with no effects
    // never resolves, so nothing can react to it.
    // Events are authored against the fight's own clock; the caller can ask for a late start,
    // in which case earlier steps are dropped and the rest rebase onto 0.
    private static void AddEvent(SimWorld world, float time, System.Action action)
    {
        if (time < UmadTimeline.StartSec) return;
        if (UmadTimeline.EndSec > 0f && time > UmadTimeline.EndSec) return;
        world.Events.Add(time - UmadTimeline.StartSec, action);
    }

    private static EnemyAction UmadHitAction(uint actionId) => new(actionId)
    {
        Effects = [AnoMech.Core.EnemyActions.EnemyActionEffects.Damage()],
    };

    public void Run(SimWorld world, int? selectedAi)
    {
        var kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: UmadConstants.BNpcBaseId.KefkaP5,
            NameId: UmadConstants.BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, -float.Pi),
            LayoutId: UmadConstants.BNpcLayoutId.KefkaP5));

        if (kefka == null) return;

        // === 过本日志实测 P5 魔导神凯夫卡读条（相对 899.13s 偏移） ===
        AddEvent(world, 3.11f,   () => kefka.Cast(UmadHitAction(0xBB40))); // 连续究极 (902.24s)
        AddEvent(world, 19.64f,  () => kefka.Cast(UmadHitAction(0xC13F))); // 洪水 (918.77s)
        AddEvent(world, 32.83f,  () => kefka.Cast(UmadHitAction(0xBB50))); // 癫狂交响曲 (931.96s)
        AddEvent(world, 54.54f,  () => kefka.Cast(UmadHitAction(0xBB42))); // 三星 (953.67s)
        AddEvent(world, 63.73f,  () => kefka.Cast(UmadHitAction(0xC24E))); // 二选一的灾祟 (962.86s)
        AddEvent(world, 75.85f,  () => kefka.Cast(UmadHitAction(0xC24E))); // 二选一的灾祟 (974.98s)
        AddEvent(world, 85.02f,  () => kefka.Cast(UmadHitAction(0xBB40))); // 连续究极 (984.15s)
        AddEvent(world, 99.55f,  () => kefka.Cast(UmadHitAction(0xBB3B))); // 混沌末世/地火 (998.68s)
        AddEvent(world, 115.71f, () => kefka.Cast(UmadHitAction(0xBB3E))); // 混沌涡旋 (1014.84s)
        AddEvent(world, 124.89f, () => kefka.Cast(UmadHitAction(0xBB50))); // 癫狂交响曲 (1024.02s)
        AddEvent(world, 149.62f, () => kefka.Cast(UmadHitAction(0xBB35))); // 遗弃末世 (1048.75s)
        AddEvent(world, 159.69f, () => kefka.Cast(UmadHitAction(0xBB38))); // 遗弃末世 (1058.82s)
        AddEvent(world, 167.89f, () => kefka.Cast(UmadHitAction(0xBB38))); // 遗弃末世 (1067.02s)
        AddEvent(world, 176.04f, () => kefka.Cast(UmadHitAction(0xBB38))); // 遗弃末世 (1075.17s)
        AddEvent(world, 184.19f, () => kefka.Cast(UmadHitAction(0xBB38))); // 遗弃末世 (1083.32s)
        AddEvent(world, 194.26f, () => kefka.Cast(UmadHitAction(0xBB3A))); // 遗弃末点/狂暴 (1093.39s)

        // 216.25f + 1s 空队列 = 217.25s（总轴 1116.38s 过本）
        AddEvent(world, 216.25f, () => { });
    }

    public void Tick(float delta, float elapsed)
    {
    }
}
