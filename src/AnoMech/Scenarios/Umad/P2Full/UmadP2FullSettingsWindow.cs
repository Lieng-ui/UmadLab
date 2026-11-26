using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Full;

public sealed class UmadP2FullSettingsWindow
{
    private static readonly (string Label, PartyRole Role)[] SupportRoles =
    [
        ("MT", PartyRole.MainTank), ("OT", PartyRole.OffTank),
        ("H1", PartyRole.RegenHealer), ("H2", PartyRole.ShieldHealer),
    ];

    private static readonly (string Label, PartyRole Role)[] DpsRoles =
    [
        ("M1", PartyRole.MeleeDpsA), ("M2", PartyRole.MeleeDpsB),
        ("R1", PartyRole.PhysRangedDps), ("R2", PartyRole.CasterDps),
    ];

    public UmadP2FullStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##umadp2full"))
        {
            SettingsGrid.FightOnlyNote();
            DrawSupportLockon();
            DrawSupportStack();
            DrawDpsStack();
            DrawFirstWings();
            SettingsGrid.End();
        }
    }

    private void DrawSupportLockon()
    {
        var v = Overrides.Forsaken.SupportLockon;
        SettingsGrid.Row("Supports:");
        if (ImGui.RadioButton("Auto##p2fsup", v == null)) Overrides.Forsaken.SupportLockon = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Chariot##p2fsup", v == LockonId.ForsakenChariot)) Overrides.Forsaken.SupportLockon = LockonId.ForsakenChariot;
        ImGui.SameLine();
        if (ImGui.RadioButton("Cone##p2fsup", v == LockonId.ForsakenCone)) Overrides.Forsaken.SupportLockon = LockonId.ForsakenCone;
    }

    private void DrawSupportStack()
    {
        var v = Overrides.Forsaken.SupportStackRole;
        SettingsGrid.Row("Support Stack:");
        if (ImGui.RadioButton("Auto##p2fsupstk", v == null)) Overrides.Forsaken.SupportStackRole = null;
        foreach (var (label, role) in SupportRoles)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{label}##p2fsupstk", v == role)) Overrides.Forsaken.SupportStackRole = role;
        }
    }

    private void DrawDpsStack()
    {
        var v = Overrides.Forsaken.DpsStackRole;
        SettingsGrid.Row("Dps Stack:");
        if (ImGui.RadioButton("Auto##p2fdpsstk", v == null)) Overrides.Forsaken.DpsStackRole = null;
        foreach (var (label, role) in DpsRoles)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{label}##p2fdpsstk", v == role)) Overrides.Forsaken.DpsStackRole = role;
        }
    }

    private void DrawFirstWings()
    {
        var v = Overrides.FirstWingsEastUnsafe;
        SettingsGrid.Row("First Wings:");
        if (ImGui.RadioButton("Auto##p2fwings", v == null)) Overrides.FirstWingsEastUnsafe = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("West Unsafe##p2fwings", v == false)) Overrides.FirstWingsEastUnsafe = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("East Unsafe##p2fwings", v == true)) Overrides.FirstWingsEastUnsafe = true;
    }

    private void ResetAll()
    {
        Overrides.Forsaken.SupportLockon = null;
        Overrides.Forsaken.SupportStackRole = null;
        Overrides.Forsaken.DpsStackRole = null;
        Overrides.FirstWingsEastUnsafe = null;
        Overrides.TrineCenterIsRight = null;
    }
}
