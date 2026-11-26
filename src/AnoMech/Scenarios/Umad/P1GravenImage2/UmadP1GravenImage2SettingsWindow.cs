using System;
using AnoMech.Core.SimObjects;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P1GravenImage2;

public sealed class UmadP1GravenImage2SettingsWindow
{
    public UmadP1GravenImage2StateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##p1gravenimage2"))
        {
            SettingsGrid.FightOnlyNote();
            DrawCountdown();
            TriStateRow("R1 Gravity:", "gi2r1grav", "DPS", "Support", Overrides.R1DpsGetsGravity, v => Overrides.R1DpsGetsGravity = v);
            TriStateRow("Ice safe:", "gi2icediag", "NE/SW", "NW/SE", Overrides.IceDiagonalB, v => Overrides.IceDiagonalB = v);
            TriStateRow("Ice orb:", "gi2icelie", "Lie", "Truth", Overrides.IceIsLie, v => Overrides.IceIsLie = v);
            TriStateRow("R2 Gravity:", "gi2r2grav", "DPS", "Support", Overrides.R2DpsGetsGravity, v => Overrides.R2DpsGetsGravity = v);
            TriStateRow("Cleave 1:", "gi2clv1", "East unsafe", "West unsafe", Overrides.Cleave1EastUnsafe, v => Overrides.Cleave1EastUnsafe = v);
            TriStateRow("Cleave 2:", "gi2clv2", "East unsafe", "West unsafe", Overrides.Cleave2EastUnsafe, v => Overrides.Cleave2EastUnsafe = v);
            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
        Overrides.CountdownSeconds = 5f;
        Overrides.R1DpsGetsGravity = null;
        Overrides.IceDiagonalB = null;
        Overrides.IceIsLie = null;
        Overrides.R2DpsGetsGravity = null;
        Overrides.Cleave1EastUnsafe = null;
        Overrides.Cleave2EastUnsafe = null;
        Overrides.Trap2Support = null;
        Overrides.Trap2Dps = null;
        Overrides.PropsBindDirector = true;
    }

    private void DrawCountdown()
    {
        var cd = Overrides.CountdownSeconds;
        SettingsGrid.Row("Pull countdown:");
        if (ImGui.RadioButton("0s##gi2cd", cd == 0f)) Overrides.CountdownSeconds = 0f;
        ImGui.SameLine();
        if (ImGui.RadioButton("5s##gi2cd", cd == 5f)) Overrides.CountdownSeconds = 5f;
        ImGui.SameLine();
        if (ImGui.RadioButton("10s##gi2cd", cd == 10f)) Overrides.CountdownSeconds = 10f;
        ImGui.SameLine();
        if (ImGui.RadioButton("15s##gi2cd", cd == 15f)) Overrides.CountdownSeconds = 15f;
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
}