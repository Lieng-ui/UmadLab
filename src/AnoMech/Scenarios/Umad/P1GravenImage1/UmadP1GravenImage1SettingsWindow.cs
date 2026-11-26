using System;
using AnoMech.Core.SimObjects;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P1GravenImage1;

public sealed class UmadP1GravenImage1SettingsWindow
{
    public UmadP1GravenImage1StateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##p1gravenimage1"))
        {
            SettingsGrid.FightOnlyNote();
            DrawCountdown();
            TriStateRow("Tether group:", "tether", "DPS", "Support", Overrides.DpsGetsTether, v => Overrides.DpsGetsTether = v);
            TriStateRow("Ice 1 safe:", "ice1diag", "NE/SW", "NW/SE", Overrides.Ice1DiagonalB, v => Overrides.Ice1DiagonalB = v);
            TriStateRow("Ice 1 orb:", "ice1lie", "Lie", "Truth", Overrides.Ice1IsLie, v => Overrides.Ice1IsLie = v);
            TriStateRow("Fire 1:", "fire1", "Stack", "Spread", Overrides.Fire1IsStack, v => Overrides.Fire1IsStack = v);
            TriStateRow("Fire 1 orb:", "fire1lie", "Lie", "Truth", Overrides.Fire1IsLie, v => Overrides.Fire1IsLie = v);
            TriStateRow("Ice 2 safe:", "ice2diag", "NE/SW", "NW/SE", Overrides.Ice2DiagonalB, v => Overrides.Ice2DiagonalB = v);
            TriStateRow("Ice 2 orb:", "ice2lie", "Lie", "Truth", Overrides.Ice2IsLie, v => Overrides.Ice2IsLie = v);
            TriStateRow("Thunder orb:", "thunlie", "Lie", "Truth", Overrides.ThunderIsLie, v => Overrides.ThunderIsLie = v);
            TriStateRow("Thunder flip:", "thunflip", "Flipped", "Normal", Overrides.ThunderOrientationFlipped, v => Overrides.ThunderOrientationFlipped = v);
            DrawThunderOffset();
            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
        Overrides.CountdownSeconds = 5f;
        Overrides.DpsGetsTether = null;
        Overrides.Ice1DiagonalB = null;
        Overrides.Ice1IsLie = null;
        Overrides.Fire1IsStack = null;
        Overrides.Fire1IsLie = null;
        Overrides.LaserTargets = null;
        Overrides.Trap1Support = null;
        Overrides.Trap1Dps = null;
        Overrides.Ice2DiagonalB = null;
        Overrides.Ice2IsLie = null;
        Overrides.ThunderRealOffset = null;
        Overrides.ThunderOrientationFlipped = null;
        Overrides.ThunderIsLie = null;
        Overrides.PropsBindDirector = true;
    }

    private void DrawCountdown()
    {
        var cd = Overrides.CountdownSeconds;
        SettingsGrid.Row("Pull countdown:");
        if (ImGui.RadioButton("0s##gi1cd", cd == 0f)) Overrides.CountdownSeconds = 0f;
        ImGui.SameLine();
        if (ImGui.RadioButton("5s##gi1cd", cd == 5f)) Overrides.CountdownSeconds = 5f;
        ImGui.SameLine();
        if (ImGui.RadioButton("10s##gi1cd", cd == 10f)) Overrides.CountdownSeconds = 10f;
        ImGui.SameLine();
        if (ImGui.RadioButton("15s##gi1cd", cd == 15f)) Overrides.CountdownSeconds = 15f;
    }

    private static void TriStateRow(string label, string id, string trueLabel, string falseLabel, bool? value, Action<bool?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto##{id}", value == null)) set(null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{trueLabel}##{id}", value == true)) set(true);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{falseLabel}##{id}", value == false)) set(false);
    }

    private void DrawThunderOffset()
    {
        var v = Overrides.ThunderRealOffset;
        SettingsGrid.Row("Thunder reals:");
        if (ImGui.RadioButton("Auto##gi1thunoff", v == null)) Overrides.ThunderRealOffset = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Slots 0,2##gi1thunoff", v == 0)) Overrides.ThunderRealOffset = 0;
        ImGui.SameLine();
        if (ImGui.RadioButton("Slots 1,3##gi1thunoff", v == 1)) Overrides.ThunderRealOffset = 1;
    }
}