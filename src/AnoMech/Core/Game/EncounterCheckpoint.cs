using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Game;

/// <summary>
/// 阶段完整存档快照：记录玩家职业量谱资源、身上 Buff 列表及全部 80 个技能冷却组状态。
/// </summary>
public sealed record EncounterCheckpoint(
    int StageIndex,
    string StageName,
    DateTime CapturedAt,
    uint ClassJobId,
    ActorGaugePacket? GaugePacket,
    StatusSlot[] Statuses,
    RecastDetail[] Recasts)
{
    public const int RecastGroupCount = 80;

    public int ActiveCooldownCount => Recasts.Count(r => r.IsActive);

    public static unsafe EncounterCheckpoint CaptureCurrent(int stageIndex, string stageName, SimPlayer? player = null)
    {
        var gauge = JobGaugeEdit.Capture();
        var jobId = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0u;

        var statuses = player is not null
            ? player.ActiveStatusSnapshot
                .Where(s => s.StatusId != 0)
                .Select(s => new StatusSlot(s.StatusId, s.Stacks, s.RemainingTime, 0u))
                .ToArray()
            : Array.Empty<StatusSlot>();

        var recasts = new RecastDetail[RecastGroupCount];
        var am = ActionManager.Instance();
        if (am != null)
        {
            for (var i = 0; i < RecastGroupCount; i++)
            {
                var detail = am->GetRecastGroupDetail(i);
                if (detail != null) recasts[i] = *detail;
            }
        }

        DiagnosticLog.Info(
            $"[Checkpoint] 已自动存档 P{stageIndex + 1} ({stageName}): job={jobId}, buffs={statuses.Length}, activeCDs={recasts.Count(r => r.IsActive)}, gauge={(gauge.HasValue ? "ok" : "none")}");

        return new EncounterCheckpoint(stageIndex, stageName, DateTime.Now, jobId, gauge, statuses, recasts);
    }

    public unsafe bool TryRestore(SimPlayer? player)
    {
        var restoredAny = false;

        if (GaugePacket is { } packet)
        {
            JobGaugeEdit.Restore(packet);
            restoredAny = true;
        }

        var am = ActionManager.Instance();
        if (am != null && Recasts.Length == RecastGroupCount)
        {
            for (var i = 0; i < RecastGroupCount; i++)
            {
                var detail = am->GetRecastGroupDetail(i);
                if (detail != null) *detail = Recasts[i];
            }
            restoredAny = true;
        }

        if (player is not null)
        {
            foreach (var cur in player.ActiveStatusSnapshot.ToArray())
            {
                if (cur.StatusId != 0)
                    player.RemoveStatus(cur.StatusId);
            }
            foreach (var s in Statuses)
            {
                if (s.StatusId == 0) continue;
                var stacks = s.Param > 0 ? (int)s.Param : 1;
                player.AddStatus(s.StatusId, s.RemainingTime, stacks, overrideStacks: true, broadcastAddEvent: false);
            }
            restoredAny = true;
        }

        DiagnosticLog.Info(
            $"[Checkpoint] 已读档恢复 P{StageIndex + 1} ({StageName}): buffs={Statuses.Length}, activeCDs={ActiveCooldownCount}");

        return restoredAny;
    }

    public bool TryRestoreGauge()
    {
        if (GaugePacket is not { } packet)
            return false;

        JobGaugeEdit.Restore(packet);
        return true;
    }
}
