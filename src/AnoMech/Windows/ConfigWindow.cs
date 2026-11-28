using System;
using System.Linq;
using System.Numerics;
using AnoMech.Multiplayer;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private string customRelayUrl;
    private string relayPassword;
    private bool askedForLocations;

    public ConfigWindow(Plugin plugin) : base("UmadLab 设置###AnoMechConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize;

        // A fixed width; the height follows the multiplayer server section, which grows when
        // the player picks their own server.
        var width = 380 * ImGuiHelpers.GlobalScale;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(width, 0), MaximumSize = new Vector2(width, float.MaxValue) };

        configuration = Plugin.Config;
        customRelayUrl = configuration.RelayServerUrl;
        relayPassword = configuration.TokenForRelay(configuration.ActiveRelayUrl());
    }

    public void Dispose() { }

    public override void Draw()
    {
        var onInn = configuration.OpenSimMenuOnInn;
        if (ImGui.Checkbox("进入旅馆自动打开主界面", ref onInn))
        {
            configuration.OpenSimMenuOnInn = onInn;
            configuration.Save();
        }

        var suppressBgm = configuration.SuppressBgm;
        if (ImGui.Checkbox("静音场景 BGM", ref suppressBgm))
        {
            configuration.SuppressBgm = suppressBgm;
            configuration.Save();
        }

        var resultMarks = configuration.EnableMechanicResultMarks;
        if (ImGui.Checkbox("显示机制成功/失败标记", ref resultMarks))
        {
            configuration.EnableMechanicResultMarks = resultMarks;
            configuration.Save();
        }

        var userActions = configuration.EnableUserActions;
        if (ImGui.Checkbox("启用自身技能结算", ref userActions))
        {
            configuration.EnableUserActions = userActions;
            configuration.Save();
            if (userActions) Natives.UserActions.Enable();
            else Natives.UserActions.Disable();
        }

        if (configuration.EnableUserActions)
        {
            var threshold = configuration.CastInterruptThreshold;
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputFloat("滑步判定窗口 (秒)", ref threshold, 0.05f, 0.1f, "%.2f"))
            {
                configuration.CastInterruptThreshold = Math.Clamp(threshold, 0f, 5f);
                configuration.Save();
            }

            var tankMitigation = configuration.EnableTankMitigation;
            if (ImGui.Checkbox("启用坦克减伤判定", ref tankMitigation))
            {
                configuration.EnableTankMitigation = tankMitigation;
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Some tankbusters (depending on scenario support) will check your mitigation.");
        }

        ImGui.Separator();

        var logging = configuration.EnableEventLogging;
        if (ImGui.Checkbox("启用事件日志", ref logging))
        {
            configuration.EnableEventLogging = logging;
            configuration.Save();
            if (logging) Plugin.LogManager.Open();
            else Plugin.LogManager.Close();
        }
        ImGui.SameLine();
        if (ImGui.Button("打开日志目录"))
            Plugin.LogManager.OpenLogsFolder();

        var actBridge = configuration.EnableActBridge;
        if (ImGui.Checkbox("ACT / 宝宝椅桥接 (Endpoint)", ref actBridge))
        {
            configuration.EnableActBridge = actBridge;
            configuration.Save();
            if (actBridge) ActLogBridge.Start();
            else ActLogBridge.Stop();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("通过 Triggernometry 自带 Endpoint 转发模拟日志（免装额外 ACT 插件，不干扰 Cactbot）。");
        if (configuration.EnableActBridge)
        {
            ImGui.SameLine();
            var status = ActLogBridge.IsConnected ? "已连通" : "等待 Endpoint";
            ImGui.TextDisabled($"({status}, {ActLogBridge.SentCount} 行)");

            var url = configuration.ActBridgeEndpointUrl ?? ActLogBridge.DefaultEndpointUrl;
            ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("Endpoint 地址##act_ep", ref url, 128))
            {
                configuration.ActBridgeEndpointUrl = url;
                configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("测试进本##act_ep_test"))
                ActLogBridge.EmitZoneEnter(1363, "妖星乱舞绝境战");
        }

        ImGui.Separator();
        DrawMultiplayerServer();

#if DEBUG
        ImGui.Separator();

        var safeMode = configuration.SafeMode;
        if (ImGui.Checkbox("Safe mode (debug)", ref safeMode))
        {
            configuration.SafeMode = safeMode;
            configuration.Save();
        }
        if (safeMode)
            ImGui.TextWrapped(
                "Safe mode cuts you off from server traffic while in the sim zone. " +
                "You won't see players joining or leaving the party, ready checks, " +
                "or duty pops.");
        else
            ImGui.TextWrapped(
                "Safe mode off — server packets reach the engine, so you'll see " +
                "party updates, ready checks, and duty pops. It's easier to break " +
                "the sim zone this way. You still can't send anything to the server " +
                "while in the instance.");
#endif
    }

    private void DrawMultiplayerServer()
    {
        ImGui.TextUnformatted("Multiplayer server");
        if (Plugin.Multiplayer.InSession)
            ImGui.TextDisabled("Changes apply the next time you host or join.");

        var activeUrl = configuration.ActiveRelayUrl();
        var valid = MultiplayerUi.IsPlausibleRelayUrl(activeUrl);
        // Only a server the player typed in, or asked about, is contacted from here: opening Settings
        // mustn't contact the default server for someone who never uses multiplayer. The Multiplayer
        // screen asks.
        var ask = configuration.UseCustomRelay || (askedForLocations && !RelayGroups.Known);
        (RelayServerStatus.Reachability State, bool RequiresPassword)? status =
            !valid ? null : ask ? RelayServerStatus.Of(activeUrl) : RelayServerStatus.Known(activeUrl);
        var needsPassword = status?.RequiresPassword ?? false;

        if (ImGui.RadioButton("Default server", !configuration.UseCustomRelay)) UseCustomRelay(false);
        if (!configuration.UseCustomRelay)
        {
            if (status is { } defaultStatus)
            {
                ImGui.SameLine();
                MultiplayerUi.DrawServerStatus(defaultStatus.State);
            }
            ImGui.Indent();
            DrawLocations(DefaultRelay.Location);
            if (needsPassword) DrawPassword();
            DrawHostOn();
            ImGui.Unindent();
        }

        if (ImGui.RadioButton("Custom server", configuration.UseCustomRelay)) UseCustomRelay(true);
        if (!configuration.UseCustomRelay) return;
        if (status is { } customStatus)
        {
            ImGui.SameLine();
            MultiplayerUi.DrawServerStatus(customStatus.State);
        }
        ImGui.Indent();
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##relayUrl", "relay.example.com:7890", ref customRelayUrl, 256))
        {
            configuration.RelayServerUrl = customRelayUrl;
            configuration.Save();
            // Shows the password saved for this server, if any. Hidden rather than cleared while
            // the address points elsewhere, so a typo doesn't lose it.
            relayPassword = configuration.TokenForRelay(customRelayUrl);
        }
        if (!valid && !string.IsNullOrWhiteSpace(customRelayUrl))
            ImGui.TextColored(MultiplayerUi.BadColor, "Doesn't look like a valid server address.");
        DrawLocations(null);
        if (needsPassword) DrawPassword();
        DrawHostOn();
        MultiplayerUi.DrawLink("How to run your own server", MultiplayerUi.RelayReadmeUrl);
        ImGui.Unindent();
    }

    // A default server may run in several locations, which only its list says: until that's
    // fetched, naming the one built in would read as the only one. Pings come from the
    // Multiplayer screen; Settings never measures them.
    private void DrawLocations(string? builtIn)
    {
        if (!RelayGroups.Known)
        {
            if (builtIn == null) return;
            if (ImGui.Button("Connect to see available locations")) askedForLocations = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Asks the default server which locations it has.");
            return;
        }
        if (RelayGroups.Current is not { Members.Length: > 1 } group)
        {
            if ((RelayGroups.Current?.Members[0].Location ?? builtIn) is { } location) ImGui.TextDisabled($"Location: {location}");
            return;
        }
        ImGui.TextDisabled("Locations:");
        foreach (var member in group.Members) ImGui.BulletText($"{member.Location}{MultiplayerUi.PingSuffix(member.Url)}");
    }

    private void DrawHostOn()
    {
        if (RelayGroups.Current is not { Members.Length: > 1 } group) return;
        const string closest = "Closest (automatic)";
        var chosen = group.Members.FirstOrDefault(m => RelayGroups.SameServer(m.Url, configuration.HostRelayUrl));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Host on");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##hostOn", chosen?.Location ?? closest))
        {
            if (ImGui.Selectable(closest, chosen is null)) SetHostOn("");
            foreach (var member in group.Members)
                if (ImGui.Selectable($"{member.Location}{MultiplayerUi.PingSuffix(member.Url)}###hostOn{member.Number}", member == chosen))
                    SetHostOn(member.Url);
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Where sessions you host run. Players who join are connected there automatically.");
    }

    private void SetHostOn(string url)
    {
        configuration.HostRelayUrl = url;
        configuration.Save();
    }

    private void UseCustomRelay(bool custom)
    {
        configuration.UseCustomRelay = custom;
        configuration.Save();
        relayPassword = configuration.TokenForRelay(configuration.ActiveRelayUrl());
    }

    private void DrawPassword()
    {
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (!ImGui.InputTextWithHint("##relayToken", "Server password", ref relayPassword, 128, ImGuiInputTextFlags.Password)) return;
        configuration.RelayAccessToken = relayPassword;
        configuration.RelayTokenOrigin = Configuration.OriginOf(configuration.ActiveRelayUrl());
        configuration.Save();
    }
}
