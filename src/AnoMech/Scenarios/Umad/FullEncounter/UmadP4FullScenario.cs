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
/// 绝妖星 P4 完整时间轴场景（735.55s ~ 899.13s，总长 163.58s）：
/// - 刷出 P4 凯夫卡（BNpcBaseId.Kefka = 18475 + BNpcLayoutId.KefkaP4）
/// - 867.81s（132.26f）变不可选中进入神凯夫卡变身转场（31.32s）
/// - 162.58f + 1s 空队列 = 163.58s（总轴 899.13s 精准切 P5）
/// </summary>
public sealed class UmadP4FullScenario : IScenario
{
    public string Name => "Umad P4 Full (MinLoop)";
    public IPhase Phase => UmadZone.P4;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => Array.Empty<IScenarioAi>();
    public float BgmSecondsAtStart => 735.55f;

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
            BNpcBaseId: UmadConstants.BNpcBaseId.Kefka,
            NameId: UmadConstants.BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, -float.Pi),
            LayoutId: UmadConstants.BNpcLayoutId.KefkaP4));

        if (kefka == null) return;

        // === 过本日志实测 P4 凯夫卡读条（相对 735.55s 偏移） ===
        AddEvent(world, 5.18f,   () => kefka.Cast(UmadHitAction(0xC2DC))); // 闹哄哄魂击 (740.73s)
        AddEvent(world, 14.74f,  () => kefka.Cast(UmadHitAction(0xBA94))); // 玄乎乎魔法 (750.29s)
        AddEvent(world, 29.71f,  () => kefka.Cast(UmadHitAction(0xBA94))); // 玄乎乎魔法 (765.26s)
        AddEvent(world, 44.87f,  () => kefka.Cast(UmadHitAction(0xBA94))); // 玄乎乎魔法 (780.42s)
        AddEvent(world, 53.03f,  () => kefka.Cast(UmadHitAction(0xC554))); // 转场动作 (788.58s)
        AddEvent(world, 71.99f,  () => kefka.Cast(UmadHitAction(0xBAA4))); // 魔法储存 (807.54s)
        AddEvent(world, 78.23f,  () => kefka.Cast(UmadHitAction(0xC5DE))); // 劈啪啪暴雷 (813.78s)
        AddEvent(world, 88.39f,  () => kefka.Cast(UmadHitAction(0xC24A))); // 扑腾腾究极 (823.94s)
        AddEvent(world, 96.41f,  () => kefka.Cast(UmadHitAction(0xBA95))); // 扩大大冰封 (831.96s)
        AddEvent(world, 107.83f, () => kefka.Cast(UmadHitAction(0xBAA5))); // 魔法放出 (843.38s)
        AddEvent(world, 127.58f, () => kefka.Cast(UmadHitAction(0xC24A))); // 扑腾腾究极 (863.13s)

        // P4 -> P5 变身转场不可选中窗口（867.81s ~ 899.13s）
        AddEvent(world, 132.26f, () => kefka.SetTargetable(false));

        // 162.58f + 1s 空队列 = 163.58s（总轴 899.13s 精准切 P5）
        AddEvent(world, 162.58f, () => { });
    }

    public void Tick(float delta, float elapsed)
    {
    }
}
