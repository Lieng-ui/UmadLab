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
/// 绝妖星 P3 双 Boss 完整时间轴场景（428.96s ~ 735.55s，总长 306.59s）：
/// - 同时刷出卡奥斯 (ChaosP3 / 19508 @ -8,0,0) 与艾克斯迪司 (Exdeath / 19509 @ +8,0,0)
/// - 不可选中窗口：
///     1) 715.38s ~ 729.26s（286.42f ~ 300.30f）：卡奥斯下场，双子短转场
///     2) 730.29s ~ 735.55s（301.33f ~ 306.59f）：艾克斯迪司下场，305.59f + 1s = 306.59s 精准切 P4
/// </summary>
public sealed class UmadP3FullScenario : IScenario
{
    public string Name => "Umad P3 Full (MinLoop)";
    public IPhase Phase => UmadZone.P3;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => Array.Empty<IScenarioAi>();
    public float BgmSecondsAtStart => 428.96f;

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
        var chaos = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: UmadConstants.BNpcBaseId.ChaosP3,
            NameId: UmadConstants.BNpcNameId.Chaos,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(new Vector3(-8f, 0f, 0f), -float.Pi),
            LayoutId: UmadConstants.BNpcLayoutId.ChaosP3));

        var exdeath = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: UmadConstants.BNpcBaseId.Exdeath,
            NameId: UmadConstants.BNpcNameId.Exdeath,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(new Vector3(8f, 0f, 0f), -float.Pi),
            LayoutId: UmadConstants.BNpcLayoutId.Exdeath));

        if (chaos == null || exdeath == null) return;

        // === 过本日志实测 P3 双 Boss 读条（相对 428.96s 偏移） ===
        AddEvent(world, 0.14f,   () => chaos.Cast(UmadHitAction(0xC2E2)));   // 决战
        AddEvent(world, 0.18f,   () => exdeath.Cast(UmadHitAction(0xC2E3))); // 决战
        AddEvent(world, 17.55f,  () => chaos.Cast(UmadHitAction(0xBAF2)));   // 深层痛楚
        AddEvent(world, 34.82f,  () => exdeath.Cast(UmadHitAction(0xBB12))); // 暴雷
        AddEvent(world, 45.94f,  () => exdeath.Cast(UmadHitAction(0xBB09))); // 暴雷
        AddEvent(world, 58.77f,  () => chaos.Cast(UmadHitAction(0xBAFD)));   // 经度聚爆
        AddEvent(world, 78.95f,  () => chaos.Cast(UmadHitAction(0xBB00)));   // 本影爆碎
        AddEvent(world, 79.13f,  () => exdeath.Cast(UmadHitAction(0xBB13))); // 真空波
        AddEvent(world, 104.50f, () => exdeath.Cast(UmadHitAction(0xBB09))); // 暴雷
        AddEvent(world, 114.47f, () => { chaos.Cast(UmadHitAction(0xC2E2)); exdeath.Cast(UmadHitAction(0xC2E3)); }); // 决战
        AddEvent(world, 121.68f, () => exdeath.Cast(UmadHitAction(0xBB09))); // 暴雷
        AddEvent(world, 126.71f, () => chaos.Cast(UmadHitAction(0xC572)));   // 地震
        AddEvent(world, 147.87f, () => exdeath.Cast(UmadHitAction(0xBAFB))); // 黑洞
        AddEvent(world, 163.27f, () => exdeath.Cast(UmadHitAction(0xBB09))); // 暴雷
        AddEvent(world, 171.34f, () => chaos.Cast(UmadHitAction(0xBB01)));   // 诅咒敕令
        AddEvent(world, 198.65f, () => chaos.Cast(UmadHitAction(0xBB01)));   // 诅咒敕令
        AddEvent(world, 204.54f, () => exdeath.Cast(UmadHitAction(0xBB09))); // 暴雷
        AddEvent(world, 239.08f, () => chaos.Cast(UmadHitAction(0xBAFE)));   // 纬度聚爆
        AddEvent(world, 239.13f, () => exdeath.Cast(UmadHitAction(0xBD66))); // 白洞
        AddEvent(world, 269.38f, () => exdeath.Cast(UmadHitAction(0xBB0F))); // 冰封
        AddEvent(world, 272.45f, () => chaos.Cast(UmadHitAction(0xBB0D)));   // 冰封
        AddEvent(world, 272.81f, () => chaos.Cast(UmadHitAction(0xBB02)));   // 轰击
        AddEvent(world, 282.76f, () => exdeath.Cast(UmadHitAction(0xBB11))); // 冰封
        AddEvent(world, 282.98f, () => chaos.Cast(UmadHitAction(0xBB05)));   // 顶起

        // 第 1 段不可选中（715.38s ~ 729.26s：卡奥斯先退场，艾克斯迪司于 729.26s 恢复收尾）
        AddEvent(world, 286.42f, () =>
        {
            chaos.SetTargetable(false);
            exdeath.SetTargetable(false);
        });
        AddEvent(world, 296.47f, () => exdeath.Cast(UmadHitAction(0xC61E))); // 陨石流星 (725.43s)
        AddEvent(world, 300.30f, () => exdeath.SetTargetable(true));  // 729.26s

        // 第 2 段不可选中（730.29s ~ 735.55s：艾克斯迪司退场 -> 切 P4 凯夫卡）
        AddEvent(world, 301.33f, () => exdeath.SetTargetable(false)); // 730.29s

        // 305.59f + 1s 空队列 = 306.59s（总轴 735.55s 精准切 P4）
        AddEvent(world, 305.59f, () => { });
    }

    public void Tick(float delta, float elapsed)
    {
    }
}
