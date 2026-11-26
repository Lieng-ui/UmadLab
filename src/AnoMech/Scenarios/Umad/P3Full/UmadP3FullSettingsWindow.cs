using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P3Full;

public sealed class UmadP3FullSettingsWindow
{
    public UmadP3FullStateOverrides Overrides { get; } = new();

    private static readonly string[] OrderLabels = ["随机 (火先/水先)", "短火长水 (19s火 / 46s水)", "短水长火 (19s水 / 46s火)"];
    private static readonly string[] ImplosionLabels = ["随机 (经度/纬度)", "经度聚爆 (前后->左右)", "纬度聚爆 (左右->前后)"];
    private static readonly string[] DirLabels = ["随机 (顺/逆时针)", "顺时针", "逆时针"];
    private static readonly string[] NumberLabels = ["随机", "1 号", "2 号", "3 号", "4 号", "5 号", "6 号", "7 号", "8 号"];

    public void Draw()
    {
        var cd = Overrides.CountdownSeconds;
        if (ImGui.SliderFloat("开怪倒数 (秒)", ref cd, 0f, 15f, "%.0f s"))
            Overrides.CountdownSeconds = cd;

        var orderIdx = Overrides.FireFirst switch { true => 1, false => 2, null => 0 };
        if (ImGui.Combo("一运火水顺序", ref orderIdx, OrderLabels, OrderLabels.Length))
            Overrides.FireFirst = orderIdx switch { 1 => true, 2 => false, _ => null };

        var impIdx = Overrides.LongitudinalFirst switch { true => 1, false => 2, null => 0 };
        if (ImGui.Combo("经纬度聚爆", ref impIdx, ImplosionLabels, ImplosionLabels.Length))
            Overrides.LongitudinalFirst = impIdx switch { 1 => true, 2 => false, _ => null };

        var dirIdx = Overrides.Clockwise switch { true => 1, false => 2, null => 0 };
        if (ImGui.Combo("麻将旋转方向", ref dirIdx, DirLabels, DirLabels.Length))
            Overrides.Clockwise = dirIdx switch { 1 => true, 2 => false, _ => null };

        var numIdx = Overrides.PlayerNumber is >= 1 and <= 8 ? Overrides.PlayerNumber.Value : 0;
        if (ImGui.Combo("自身麻将序号", ref numIdx, NumberLabels, NumberLabels.Length))
            Overrides.PlayerNumber = numIdx == 0 ? null : numIdx;

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
