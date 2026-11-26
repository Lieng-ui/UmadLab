using System;
using AnoMech.Core;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

/// <summary>
/// Plan B（R8）：日志精度升级为 3 位小数（:F3），数值逻辑保持连续浮点 target = baseSec * 0.87f 不变。
/// </summary>
public static unsafe class FukaGcdFix
{
    private const uint FukaStatusId = 1299; // 0x513 风花
    private const int GcdRecastGroup = 57;  // 武士标准 GCD 冷却组
    private const uint StandardGcdActionId = 0x1D38; // 雪风，用来取基础 GCD

    public static void ApplyIfGcd(uint actionId)
    {
        var am = ActionManager.Instance();
        var lp = Plugin.ObjectTable.LocalPlayer;
        if (am == null || lp == null)
            return;

        int group = am->GetRecastGroup((int)ActionType.Action, actionId);
        bool hasFuka = false;
        foreach (var s in lp.StatusList)
        {
            if (s.StatusId == FukaStatusId)
            {
                hasFuka = true;
                break;
            }
        }
        float groupRecast = am->GetRecastTimeForGroup(group);

        DiagnosticLog.Info(
            $"[umadenc gcd-in] action=0x{actionId:X} enabled={Plugin.Config.EnableFukaGcdFix} " +
            $"group={group} groupRecast={groupRecast:F3} hasFuka={hasFuka}");

        if (!Plugin.Config.EnableFukaGcdFix) return;
        if (group != GcdRecastGroup) return;
        if (!hasFuka) return;

        float baseSec = ActionManager.GetAdjustedRecastTime(ActionType.Action, actionId) / 1000f;
        if (baseSec <= 0f)
            return;

        float target = baseSec * (1f - 0.13f);
        var d = am->GetRecastGroupDetail(group);
        float elapsedNow = (d != null && d->IsActive) ? d->Elapsed : 0f;

        am->SetCooldown(ActionType.Action, actionId, elapsedNow, target);

        var d2 = am->GetRecastGroupDetail(group);
        float afterApi = d2 != null ? d2->Total : -1f;

        if (d2 != null)
            d2->Total = target;
        float afterPoke = d2 != null ? d2->Total : -1f;

        DiagnosticLog.Info(
            $"[umadenc gcd] action=0x{actionId:X} base={baseSec:F3} target={target:F3} " +
            $"apiTotal={afterApi:F3} pokedTotal={afterPoke:F3}");
    }

    /// <summary>
    /// 只在风花「新获得」的那一帧打印一次当前 GCD，之后每重新获得再打印一次（不跟每次 GCD 走）。
    /// </summary>
    public static void Tick()
    {
        var lp = Plugin.ObjectTable.LocalPlayer;
        if (lp == null)
        {
            lastHasFuka = false;
            return;
        }

        bool hasFuka = false;
        foreach (var s in lp.StatusList)
        {
            if (s.StatusId == FukaStatusId)
            {
                hasFuka = true;
                break;
            }
        }

        if (hasFuka && !lastHasFuka)
            PrintFukaGain();

        lastHasFuka = hasFuka;
    }

    private static void PrintFukaGain()
    {
        float baseSec = ActionManager.GetAdjustedRecastTime(ActionType.Action, StandardGcdActionId) / 1000f;
        if (baseSec <= 0f)
        {
            var am0 = ActionManager.Instance();
            baseSec = am0 != null ? am0->GetRecastTimeForGroup(GcdRecastGroup) : 0f;
        }

        float target = baseSec * (1f - 0.13f);

        Plugin.ChatGui.Print(
            $"[umadenc] 风花生效 GCD {target:F3}s / {baseSec:F3}s");
        DiagnosticLog.Info(
            $"[umadenc fuka] gained base={baseSec:F3} target={target:F3} " +
            $"enabled={Plugin.Config.EnableFukaGcdFix}");
    }

    private static bool lastHasFuka;
}
