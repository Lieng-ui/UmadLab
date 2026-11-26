using System;
using AnoMech.Core;
using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions.Jobs;

/// <summary>
/// R8（功能 A）：武士默想（Meditate）每 3 秒周期性攒资源（+10 剑气 +1 层剑压）。
/// 由 TimedGaugeHandler 现有 Tick / OnAction 驱动，不另起任何定时器。
/// ID 已按过本日志核对：
/// - 动作 7497 (0x1D49) = 默想 (Meditate)
/// - 状态 1231 (0x04CF) = 默想（持续 15s；注：1232 / 0x04D0 为心眼）
/// </summary>
public static unsafe class SamuraiMeditateTick
{
    public const uint MeditateActionId = 7497;   // 0x1D49 默想
    public const ushort MeditateStatusId = 1231; // 0x04CF 默想状态
    private const float MeditateDurationSec = 15.0f;
    private const float TickIntervalSec = 3.0f;

    private static float accumulator;

    public static void Reset()
    {
        accumulator = 0f;
    }

    /// <summary>
    /// 挂在 TimedGaugeHandler.OnAction（或任意玩家动作入口）：
    /// - 释放默想(7497)：重置 3s 计时器并确保挂上 15s 默想状态(1231)
    /// - 释放其它任何技能：立刻中断默想状态(1231)并清零计时器
    /// </summary>
    public static void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        if (PlayerJob.Current != JobId.Samurai) return;

        var player = Plugin.Game?.Player;
        if (player == null) return;

        if (actionId == MeditateActionId)
        {
            accumulator = 0f;
            if (!player.HasStatus(MeditateStatusId))
                player.AddStatus(MeditateStatusId, MeditateDurationSec);

            DiagnosticLog.Info("[umadenc meditate] started (status 1231, 15s)");
            return;
        }

        // 被任何其它技能打断即停
        if (player.HasStatus(MeditateStatusId))
        {
            player.RemoveStatus(MeditateStatusId);
            accumulator = 0f;
            DiagnosticLog.Info($"[umadenc meditate] interrupted by action=0x{actionId:X}");
        }
    }

    /// <summary>
    /// 挂在 TimedGaugeHandler.Tick(float delta)：状态 1231 存在时每 3 秒 +10 剑气 +1 剑压。
    /// </summary>
    public static void Tick(float delta)
    {
        if (delta <= 0f || PlayerJob.Current != JobId.Samurai)
        {
            accumulator = 0f;
            return;
        }

        var player = Plugin.Game?.Player;
        if (player == null || !player.HasStatus(MeditateStatusId))
        {
            accumulator = 0f;
            return;
        }

        accumulator += delta;
        if (accumulator < TickIntervalSec)
            return;

        accumulator -= TickIntervalSec;

        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;

        byte nextKenki = (byte)Math.Min(100, jgm->Samurai.Kenki + 10);
        byte nextMed = (byte)Math.Min(3, jgm->Samurai.MeditationStacks + 1);
        jgm->Samurai.Kenki = nextKenki;
        jgm->Samurai.MeditationStacks = nextMed;

        DiagnosticLog.Info($"[umadenc meditate] tick +10 kenki -> {nextKenki}, +1 med -> {nextMed}");
    }
}
