using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.Game;

/// <summary>
/// 绝妖星武士排轴诊断工具：GCD 只读诊断 + Kaeshi 字节标定（写 gauge，用后即弃）。
/// </summary>
public static unsafe class UmadDiagnostics
{
    /// <summary>
    /// 在 Status 表里按名字子串搜索状态 ID（例如 /umadenc find 彼岸花）。
    /// </summary>
    public static void FindStatus(string needle)
    {
        var hits = new System.Collections.Generic.List<string>();
        for (ushort id = 1; id < 6000; id++)
        {
            var name = Natives.Data.StatusName(id);
            if (name == id.ToString()) continue;
            if (name.Contains(needle, System.StringComparison.Ordinal))
                hits.Add($"{id}={name}");
        }

        var text = hits.Count == 0
            ? $"[umadenc] 未找到包含「{needle}」的状态"
            : $"[umadenc] {hits.Count} 个匹配: {string.Join(", ", hits.GetRange(0, System.Math.Min(12, hits.Count)))}";
        DiagnosticLog.Info(text);
        Plugin.ChatGui.Print(text);
    }

    /// <summary>
    /// 探针：给本地玩家挂一个状态，用来确认"哪个状态会让回返图标变化"。
    /// </summary>
    public static void ApplyStatusProbe(int statusId)
    {
        var player = Plugin.Game?.Player;
        if (player == null)
        {
            Plugin.ChatGui.Print("[umadenc] 未在模拟场景内");
            return;
        }

        player.AddStatus((ushort)statusId, 10f);
        DiagnosticLog.Info($"[umadenc probe] applied status {statusId} for 10s");
        Plugin.ChatGui.Print($"[umadenc] 已挂状态 {statusId}（10 秒），看燕返图标变了没");
    }

    /// <summary>
    /// 标定命令：把 SamuraiGauge.Kaeshi 直接写成指定值，用于确定"哪个数值对应哪个回返图标"。
    /// 用户观察快捷栏"燕返"位置显示的图标即可建立映射。
    /// </summary>
    public static void SetKaeshi(int value)
    {
        if (value is < 0 or > 4)
        {
            Plugin.ChatGui.Print("[umadenc] 用法: /umadenc kaeshi 0|1|2|3|4");
            return;
        }

        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;

        byte before = (byte)jgm->Samurai.Kaeshi;
        jgm->Samurai.Kaeshi = (KaeshiAction)value;
        byte after = (byte)jgm->Samurai.Kaeshi;

        DiagnosticLog.Info($"[umadenc kaeshi-set] {before} -> {after}");
        Plugin.ChatGui.Print($"[umadenc] Kaeshi {before} -> {after}（看燕返位置的图标）");
    }

    private const uint YukikazeActionId = 0x1D38; // 雪风（标准武士 GCD）
    private const uint FukaStatusId = 1299;       // 0x513 风花（-13% GCD）
    private const uint FugetsuStatusId = 1298;    // 0x512 风月（+13% 伤害）

    public static void DumpFukaGcdDiag()
    {
        var am = ActionManager.Instance();
        var lp = Plugin.ObjectTable.LocalPlayer;
        if (am == null || lp == null)
        {
            const string err = "[umadenc diag] ActionManager 或 LocalPlayer 为空，请进入游戏后再执行。";
            Plugin.Log.Warning(err);
            Plugin.ChatGui.Print(err);
            return;
        }

        // 1) 客户端权威复唱计算（静态方法，返回毫秒，如 2140 或 2500）与当前 RecastTime（秒）
        int adjMs = ActionManager.GetAdjustedRecastTime(ActionType.Action, YukikazeActionId);
        float raw = am->GetRecastTime(ActionType.Action, YukikazeActionId);

        // 2) 检查本地玩家 StatusList 是否存在风花(1299) / 风月(1298)
        bool hasFuka = false;
        float fukaRem = 0f;
        bool hasFugetsu = false;
        foreach (var s in lp.StatusList)
        {
            if (s.StatusId == FukaStatusId)
            {
                hasFuka = true;
                fukaRem = s.RemainingTime;
            }
            else if (s.StatusId == FugetsuStatusId)
            {
                hasFugetsu = true;
            }
        }

        // 3) 读取雪风实际所属 GCD 冷却组（由 GetRecastGroup 动态查询，避免 57/58 下标偏差）
        int gcdGroup = am->GetRecastGroup((int)ActionType.Action, YukikazeActionId);
        var detail = am->GetRecastGroupDetail(gcdGroup);
        float groupTotal = detail != null ? detail->Total : -1f;
        float groupElapsed = detail != null ? detail->Elapsed : -1f;
        bool groupActive = detail != null && detail->IsActive;

        string msg =
            $"[umadenc diag] adjMs={adjMs} ({adjMs / 1000f:F2}s) raw={raw:F2}s " +
            $"hasFuka={hasFuka}({fukaRem:F1}s) hasFugetsu={hasFugetsu} " +
            $"gcdGroup={gcdGroup} total={groupTotal:F2}s elapsed={groupElapsed:F2}s active={groupActive}";

        Plugin.Log.Info(msg);
        Plugin.ChatGui.Print(msg);
    }
}
