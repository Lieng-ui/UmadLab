using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P1Full;

public sealed class UmadP1FullSettingsWindow
{
    public UmadP1FullStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        var countdown = Overrides.CountdownSeconds;
        if (ImGui.SliderFloat("开怪倒计时（秒）", ref countdown, 0f, 15f, "%.0f s"))
            Overrides.CountdownSeconds = countdown;

        var bindDirector = Overrides.PropsBindDirector;
        if (ImGui.Checkbox("石像与黑泥绑定副本控制器 (PropsBindDirector)", ref bindDirector))
            Overrides.PropsBindDirector = bindDirector;
    }
}
