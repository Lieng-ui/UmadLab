using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P3Full;

public sealed class UmadP3FullSettingsWindow
{
    public UmadP3FullStateOverrides Overrides { get; } = new();

    private static readonly string[] NumberLabels = ["随机", "1 号", "2 号", "3 号", "4 号", "5 号", "6 号", "7 号", "8 号"];
    private static readonly string[] TetherPriorityLabels = ["DHT (D1-4 > H1-2 > MT > ST)", "DHT (D1-4 > H1-2 > ST > MT)", "DTN (D1-4 > MT > ST > H1-2)"];

    public void Draw()
    {
        var cd = Overrides.CountdownSeconds;
        if (ImGui.SliderFloat("开怪倒数 (秒)", ref cd, 0f, 15f, "%.0f s"))
            Overrides.CountdownSeconds = cd;

        var numIdx = Overrides.PlayerNumber is >= 1 and <= 8 ? Overrides.PlayerNumber.Value : 0;
        if (ImGui.Combo("自身麻将序号", ref numIdx, NumberLabels, NumberLabels.Length))
            Overrides.PlayerNumber = numIdx == 0 ? null : numIdx;

        var prioIdx = (int)Overrides.TetherPriority;
        if (ImGui.Combo("二运接线优先级", ref prioIdx, TetherPriorityLabels, TetherPriorityLabels.Length))
            Overrides.TetherPriority = (BlackHoleTetherPriority)prioIdx;

        var chaosDelay = Overrides.ChaosKillDelayAfterBigBangSec;
        if (ImGui.SliderFloat("顶起后卡奥斯击杀 (秒)", ref chaosDelay, 5.0f, 25.0f, "+%.1f s"))
            Overrides.ChaosKillDelayAfterBigBangSec = chaosDelay;

        var exDelay = Overrides.ExdeathKillDelayAfterChaosSec;
        if (ImGui.SliderFloat("卡奥斯死后小艾击杀 (秒)", ref exDelay, 2.0f, 9.5f, "+%.1f s"))
            Overrides.ExdeathKillDelayAfterChaosSec = exDelay;

        var cheat = Overrides.EnableCheatSheet;
        if (ImGui.Checkbox("开启 P3 小抄 (麻将 + 二运接线)##umadp3full_cheat", ref cheat))
            Overrides.EnableCheatSheet = cheat;
    }
}
