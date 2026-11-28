using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P2Full;

public sealed class UmadP2FullSettingsWindow
{
    public UmadP2FullStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        var cd = Overrides.CountdownSeconds;
        if (ImGui.SliderFloat("开怪倒数 (秒)##p2full_cd", ref cd, 0f, 15f, "%.0f s"))
            Overrides.CountdownSeconds = cd;
    }
}
