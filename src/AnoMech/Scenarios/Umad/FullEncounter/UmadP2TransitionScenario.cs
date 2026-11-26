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
/// 绝妖星 P2 完整时间轴场景（209.38s ~ 428.96s，总长 219.58s）：
/// - 209.38s（0.00f）刷出 P2 凯夫卡（KefkaP3 / 19504）立即可选中
/// - 375.64s（166.26f）进入 P2→P3 大转场不可选中窗口（53.32s）
/// - 218.58f 排入末事件，经 1s 空队列判定后于 219.58s（总轴 428.96s）精准切入 P3 双 Boss
/// </summary>
public sealed class UmadP2TransitionScenario : IScenario
{
    public string Name => "Umad P2 Transition (MinLoop)";
    public IPhase Phase => UmadZone.P2;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => Array.Empty<IScenarioAi>();
    public float BgmSecondsAtStart => 209.38f;

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
            BNpcBaseId: UmadConstants.BNpcBaseId.KefkaP3,
            NameId: UmadConstants.BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, -float.Pi),
            LayoutId: UmadConstants.BNpcLayoutId.KefkaP3));

        if (kefka == null) return;

        // === 过本日志实测 P2 完整读条（相对 209.38s 偏移） ===
        AddEvent(world, 7.23f,   () => kefka.Cast(UmadHitAction(0xC24C))); // 终末双腕 (216.61s)
        AddEvent(world, 20.47f,  () => kefka.Cast(UmadHitAction(0xBABC))); // 遗弃末世 (229.85s)
        AddEvent(world, 43.62f,  () => kefka.Cast(UmadHitAction(0xBAD3))); // 过去终结 (253.00s)
        AddEvent(world, 56.39f,  () => kefka.Cast(UmadHitAction(0xBADD))); // 消灭之脚 (265.77s)
        AddEvent(world, 64.59f,  () => kefka.Cast(UmadHitAction(0xBAD2))); // 未来终结 (273.97s)
        AddEvent(world, 77.30f,  () => kefka.Cast(UmadHitAction(0xBADC))); // 消灭之脚 (286.68s)
        AddEvent(world, 85.55f,  () => kefka.Cast(UmadHitAction(0xBAD2))); // 未来终结 (294.93s)
        AddEvent(world, 98.25f,  () => kefka.Cast(UmadHitAction(0xBADC))); // 消灭之脚 (307.63s)
        AddEvent(world, 106.69f, () => kefka.Cast(UmadHitAction(0xBAD3))); // 过去终结 (316.07s)
        AddEvent(world, 119.38f, () => kefka.Cast(UmadHitAction(0xBADD))); // 消灭之脚 (328.76s)
        AddEvent(world, 128.86f, () => kefka.Cast(UmadHitAction(0xBABD))); // 制裁之光 (338.24s)
        AddEvent(world, 142.08f, () => kefka.Cast(UmadHitAction(0xBADF))); // 异三角 (351.46s)
        AddEvent(world, 148.22f, () => kefka.Cast(UmadHitAction(0xBACE))); // 破坏之翼 (357.60s)
        AddEvent(world, 158.38f, () => kefka.Cast(UmadHitAction(0xC487))); // 破坏之翼 (367.76s)
        AddEvent(world, 164.48f, () => kefka.Cast(UmadHitAction(0xC24C))); // 终末双腕 (373.86s)

        // P2 -> P3 大转场不可选中窗口（375.64s ~ 428.96s，53.32s）
        AddEvent(world, 166.26f, () => kefka.SetTargetable(false));
        AddEvent(world, 175.97f, () => kefka.Cast(UmadHitAction(0xC3F7))); // 疼飕飕暴风 (385.35s)
        AddEvent(world, 212.56f, () => kefka.Cast(UmadHitAction(0xBAE2))); // 转场读条 (421.94s)

        // 218.58f + 1s 空队列 = 219.58s（428.96s 精准切 P3）
        AddEvent(world, 218.58f, () => { });
    }

    public void Tick(float delta, float elapsed)
    {
    }
}
