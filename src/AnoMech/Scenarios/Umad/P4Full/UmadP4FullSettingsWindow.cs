using System;
using AnoMech.Scenarios.Umad.P4KefkaSays;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P4Full;

public sealed class UmadP4FullSettingsWindow
{
    public UmadP4FullStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("重置默认##umadp4full"))
        {
            Overrides.CountdownSeconds = 5.0f;
            Overrides.EnableCheatSheet = true;
            Overrides.KefkaSays.ExdeathCast1Real = null;
            Overrides.KefkaSays.ExdeathCast2 = ExdeathCast2Mode.Auto;
            Overrides.KefkaSays.ExdeathCast3Real = null;
            Overrides.KefkaSays.ExdeathCast4Real = null;
            Overrides.KefkaSays.ChaosCast1Real = null;
            Overrides.KefkaSays.ChaosCast2Real = null;
        }

        var cd = Overrides.CountdownSeconds;
        if (ImGui.SliderFloat("开怪倒数 (秒)##umadp4full_cd", ref cd, 0f, 15f, "%.0f 秒"))
            Overrides.CountdownSeconds = MathF.Round(cd);

        var cs = Overrides.EnableCheatSheet;
        if (ImGui.Checkbox("启用 P4 真假小抄##umadp4full_cs", ref cs))
            Overrides.EnableCheatSheet = cs;

        if (SettingsGrid.Begin("##umadp4full"))
        {
            RealFakeRow("新小艾 1 (一运):", "p4ex1", Overrides.KefkaSays.ExdeathCast1Real, v => Overrides.KefkaSays.ExdeathCast1Real = v);
            DrawExdeath2();
            RealFakeRow("新小艾 3 (三运):", "p4ex3", Overrides.KefkaSays.ExdeathCast3Real, v => Overrides.KefkaSays.ExdeathCast3Real = v);
            RealFakeRow("新小艾 4 (黑白):", "p4ex4", Overrides.KefkaSays.ExdeathCast4Real, v => Overrides.KefkaSays.ExdeathCast4Real = v);
            RealFakeRow("卡奥斯 1 (水火):", "p4ch1", Overrides.KefkaSays.ChaosCast1Real, v => Overrides.KefkaSays.ChaosCast1Real = v);
            RealFakeRow("卡奥斯 2 (水火):", "p4ch2", Overrides.KefkaSays.ChaosCast2Real, v => Overrides.KefkaSays.ChaosCast2Real = v);
            SettingsGrid.End();
        }
    }

    private static void RealFakeRow(string label, string id, bool? value, Action<bool?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"随机##{id}", value == null)) set(null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"真##{id}", value == true)) set(true);
        ImGui.SameLine();
        if (ImGui.RadioButton($"假##{id}", value == false)) set(false);
    }

    private void DrawExdeath2()
    {
        var v = Overrides.KefkaSays.ExdeathCast2;
        SettingsGrid.Row("新小艾 2 (二运):");
        if (ImGui.RadioButton("随机##p4ex2", v == ExdeathCast2Mode.Auto)) Overrides.KefkaSays.ExdeathCast2 = ExdeathCast2Mode.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("真##p4ex2", v == ExdeathCast2Mode.Real)) Overrides.KefkaSays.ExdeathCast2 = ExdeathCast2Mode.Real;
        ImGui.SameLine();
        if (ImGui.RadioButton("假##p4ex2", v == ExdeathCast2Mode.Fake)) Overrides.KefkaSays.ExdeathCast2 = ExdeathCast2Mode.Fake;
        ImGui.SameLine();
        if (ImGui.RadioButton("与1相反##p4ex2", v == ExdeathCast2Mode.OppositeTo1)) Overrides.KefkaSays.ExdeathCast2 = ExdeathCast2Mode.OppositeTo1;
    }
}
