using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Multiplayer;
using AnoMech.Scenarios.Umad.FullEncounter;
using AnoMech.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace AnoMech;

public sealed class Plugin : IAsyncDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/anomech";
    private const string CommandAlias = "/ulab";
    private bool commandsRegistered;

    internal static Configuration Config { get; private set; } = null!;
    internal static Game Game { get; private set; } = null!;
    internal static UmadEncounterChain UmadChain { get; private set; } = null!;
    internal static PracticeLoop Practice { get; private set; } = null!;
    internal static MultiplayerManager Multiplayer { get; private set; } = null!;
    internal RunController Runs { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("AnoMech");

    // Plugin-lifetime singletons, static like the PluginServices above so sim code reaches them
    // without a Plugin reference.
    internal static LogManager LogManager { get; private set; } = null!;
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private static UmadEncounterChainWindow UmadChainWindow { get; set; } = null!;
    private FailureBanner? FailureBanner { get; init; }
#if DEBUG
    private DamageDebugWindow DamageDebugWindow { get; init; }
#endif

    private OpcodeUpdater? opcodeUpdater;

    public Plugin()
    {
        // First, so every subsequent construction step's own logging is captured from the start.
        DiagnosticLog.Initialize();
        try
        {
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            LogManager = new LogManager();
            if (Config.EnableEventLogging) LogManager.Open();
            if (Config.EnableActBridge) ActLogBridge.Start();

            GameNatives.Install();
            opcodeUpdater = new OpcodeUpdater();
            Game = new Game();
            UmadChain = new UmadEncounterChain(Game, msg => ChatGui.Print(msg));
            UmadChainWindow = new UmadEncounterChainWindow(UmadChain);
            Practice = new PracticeLoop(Game);
            Multiplayer = new MultiplayerManager();
            Runs = new RunController(Game, Multiplayer);
            if (Config.EnableUserActions) Natives.UserActions.Enable();
            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);

            windowSystem.AddWindow(ConfigWindow);
            windowSystem.AddWindow(MainWindow);
            windowSystem.AddWindow(MainWindow.ScenarioPanel);
            windowSystem.AddWindow(MainWindow.PartyPanel);
            windowSystem.AddWindow(UmadChainWindow);
            FailureBanner = new FailureBanner();
            windowSystem.AddWindow(FailureBanner);
#if DEBUG
            DamageDebugWindow = new DamageDebugWindow(this);
            windowSystem.AddWindow(DamageDebugWindow);
#endif

            if (Config.OpenSimMenuOnInn && ZoneControl.IsInInn())
                MainWindow.IsOpen = true;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open UmadLab. Subcommands: config, mp, start, reset, leave"
            });
            CommandManager.AddHandler("/umadenc", new CommandInfo(OnUmadEncCommand)
            {
                HelpMessage = "打开串联器"
            });
            CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
            {
                HelpMessage = "打开 UmadLab（同 /anomech）"
            });
            commandsRegistered = true;

            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            Framework.Update += OnFrameworkUpdate;

            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            ClientState.TerritoryChanged += OnTerritoryChanged;
            ClientState.Logout += OnLogout;
            DutyState.DutyStarted += OnDutyStarted;
            DutyState.DutyWiped += OnDutyWiped;
            DutyState.DutyCompleted += OnDutyCompleted;

            // Initialize Pointers
            EventObjectManagerPointers.Initialize();
            GameMainPointers.Initialize();
            ModelContainerPointers.Initialize();
            PacketDispatcherPointers.Initialize();
            ResolvedStringPointers.Initialize();

            Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
            // The diagnostic log's own header is written before any hook exists, so it alone
            // doesn't prove a load succeeded.
            DiagnosticLog.Info("[Plugin] constructed OK -- all hooks and pointers initialized.");
        }
        catch (Exception)
        {
            // Dalamud never disposes an instance whose constructor threw; the log writer's open
            // handle would then block every later load from rotating the active log.
            Log.Warning("[Plugin] Load failed during construction -- tearing down partial state.");
            try
            {
                Dispose();
            }
            catch (Exception teardown)
            {
                Log.Warning($"[Plugin] Partial teardown after failed load threw: {teardown.Message}");
            }
            throw;
        }
    }

    public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Dalamud keeps this plugin's services and hooks until this completes, so an unload during a
    // stay leaves it as the Leave button does and frames tick on until the guarded lift is done.
    public async ValueTask DisposeAsync()
    {
        var since = Stopwatch.GetTimestamp();
        try
        {
            while (!await Framework.RunOnFrameworkThread(() => DisposeOnceOutOfStay(since)))
                await Framework.DelayTicks(1);
        }
        catch (OperationCanceledException)
        {
            // The game began closing mid-wait.
            await Framework.RunOnFrameworkThread(Dispose);
        }
    }

    // Leaving and the check share one callback, so no tick can start another stay in between.
    // The lift dies by itself well inside the timeout when it can't verify the inn.
    private bool DisposeOnceOutOfStay(long since)
    {
        if (ZoneControl.Current is { } zone && !Framework.IsFrameworkUnloading
            && Stopwatch.GetElapsedTime(since) < UnloadLiftTimeout)
        {
            if (zone.IsActive)
            {
                Log.Information("[Plugin] Unloaded during a stay -- leaving it before teardown.");
                Game?.LeaveNow();
                zone.Revert();
            }
            if (zone.GuardArmed) return false;
        }
        Dispose();
        return true;
    }

    private static readonly TimeSpan UnloadLiftTimeout = TimeSpan.FromSeconds(10);

    // Null-tolerant throughout: the constructor's failure path calls this on a half-built
    // instance, where anything past the throwing step was never assigned.
    private void Dispose()
    {
        try
        {
            DisposeSubsystems();
        }
        finally
        {
            // Dalamud disposes this plugin's hooks once Dispose returns or throws, so a run must be
            // stopped or lifted by then even when a step above threw. A no-op after a clean Game.Dispose.
            ZoneControl.Current?.Dispose();
        }

        // Last, so it captures every other subsystem's teardown logging before the DLL unloads.
        ActLogBridge.Stop();
        DiagnosticLog.Shutdown();
    }

    private void DisposeSubsystems()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.Logout -= OnLogout;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyWiped -= OnDutyWiped;
        DutyState.DutyCompleted -= OnDutyCompleted;

        windowSystem.RemoveAllWindows();

        TimelineDebug.Shutdown();
        CommandManager.RemoveHandler("/umadenc");
        UmadChain?.Dispose();
        UmadChainWindow?.Dispose();
        Multiplayer?.Dispose();
        Game?.Dispose();
        Natives.Dispose();
        opcodeUpdater?.Dispose();
        LogManager?.Dispose();
        ConfigWindow?.Dispose();
        MainWindow?.Dispose();
        FailureBanner?.Dispose();
#if DEBUG
        DamageDebugWindow?.Dispose();
#endif

        if (commandsRegistered)
        {
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(CommandAlias);
        }
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        // FrameDeltaTime, not framework.UpdateDelta: UpdateDelta is wall-clock
        // truncated to whole ms, so summing it drifts. FrameDeltaTime is the
        // full-precision delta the game ticks its own animations with.
        var fw = CSFramework.Instance();
        if (fw == null) return;
        // First: the zone guard must run when Game.Tick is paused or throwing.
        Natives.Tick(fw->FrameDeltaTime);
        // Both ticks reach code driven by whatever a relay sent; neither may take the frame
        // pump down.
        try { Runs.Tick(); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Runs.Tick threw: {e}"); }
        try { Game.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Game.Tick threw: {e}"); }
        try { UmadChain.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] UmadChain.Tick threw: {e}"); }
#if DEBUG
        try { DamageDebugWindow.Instance?.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] DamageDebugWindow.Tick threw: {e}"); }
#endif
        try
        {
            // Every frame, not just while the settings are drawn: peers read what the host has
            // selected whether or not its window is open.
            if (Multiplayer.SessionCode != null)
            {
                Multiplayer.PublishSelectedScenario(MainWindow.SelectedScenario);
                Multiplayer.PublishScenarioSettings(MainWindow.SelectedScenario);
            }
            Multiplayer.Tick(fw->FrameDeltaTime);
        }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Multiplayer.Tick threw: {e}"); }
    }

    private void OnTerritoryChanged(uint territory)
    {
        ZoneControl.NoteTerritoryChanged(territory);
        var row = DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territory);
        var isInn = row?.TerritoryIntendedUse.RowId == 2; // TerritoryIntendedUse.Inn
        if (!isInn)
        {
            var name = row?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            LogManager.LogEnterInstance(territory, name);
        }

        if (!isInn)
        {
            MainWindow.IsOpen = false;
            return;
        }
        if (Config.OpenSimMenuOnInn)
            MainWindow.IsOpen = true;
    }

    private void OnLogout(int type, int code) => ZoneControl.NoteLogout(type, code);

    private void OnDutyStarted(IDutyStateEventArgs args)
        => LogManager.LogCombatStart(args.TerritoryType.RowId);

    private void OnDutyWiped(IDutyStateEventArgs args)
        => LogManager.LogCombatEnd(args.TerritoryType.RowId, wipe: true);

    private void OnDutyCompleted(IDutyStateEventArgs args)
        => LogManager.LogCombatEnd(args.TerritoryType.RowId, wipe: false);

    private static void OnUmadEncCommand(string command, string args)
    {
        var tokens = args.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens[0] == "ui")
        {
            UmadChainWindow.Toggle();
            return;
        }

        bool? soloOverride = System.Linq.Enumerable.Contains(tokens, "solo") ? true
            : System.Linq.Enumerable.Contains(tokens, "full") ? false
            : null;
        float? countdownOverride = null;
        AnoMech.Core.Game.Party.PartyRole? roleOverride = null;
        bool hasRoleOverride = false;

        foreach (var token in tokens)
        {
            if (token.StartsWith("cd=", StringComparison.Ordinal) &&
                float.TryParse(token[3..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var inlineCd))
                countdownOverride = inlineCd;
            else if (UmadEncounterChain.TryParseRole(token, out var parsedRole))
            {
                roleOverride = parsedRole;
                hasRoleOverride = true;
            }
        }

        UmadTimeline.StartSec = ParseSeconds(tokens);
        switch (tokens[0])
        {
            case "start":
            case "p1":
                UmadChain.StartFromPhase(0, soloOverride, roleOverride, hasRoleOverride, countdownOverride);
                break;
            case "p2":
                UmadChain.StartFromPhase(1, soloOverride, roleOverride, hasRoleOverride, countdownOverride);
                break;
            case "p3":
                UmadChain.StartFromPhase(2, soloOverride, roleOverride, hasRoleOverride, countdownOverride);
                break;
            case "p4":
                UmadChain.StartFromPhase(3, soloOverride, roleOverride, hasRoleOverride, countdownOverride);
                break;
            case "p5":
                UmadChain.StartFromPhase(4, soloOverride, roleOverride, hasRoleOverride, countdownOverride);
                break;
            case "role":
                if (tokens.Length >= 2 && UmadEncounterChain.TryParseRole(tokens[1], out var targetRole))
                {
                    UmadChain.SelectedRole = targetRole;
                    ChatGui.Print($"[UmadChain] 玩家位置已设为: {UmadChain.FormatRoleLabel()}");
                }
                else
                    ChatGui.Print("[UmadChain] 用法: /umadenc role auto|mt|st|h1|h2|d1|d2|d3|d4");
                break;
            case "cd":
                if (tokens.Length >= 2 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cdSec))
                {
                    UmadChain.CountdownSeconds = Math.Clamp(cdSec, 0f, 30f);
                    ChatGui.Print($"[UmadChain] 开怪倒数已设为: {UmadChain.CountdownSeconds:F0} 秒");
                }
                else
                    ChatGui.Print("[UmadChain] 用法: /umadenc cd <秒数>");
                break;
            case "stop":
                UmadChain.Stop();
                break;
            case "diag":
                UmadDiagnostics.DumpFukaGcdDiag();
                break;
            case "gcd":
                Config.EnableFukaGcdFix = tokens.Length >= 2 ? tokens[1] == "on" : !Config.EnableFukaGcdFix;
                Config.Save();
                ChatGui.Print($"[UmadEnc] 风花 GCD 本地修复已{(Config.EnableFukaGcdFix ? "开启" : "关闭")}");
                break;
            case "kaeshi":
                if (tokens.Length >= 2 && int.TryParse(tokens[1], out var kvalue))
                    UmadDiagnostics.SetKaeshi(kvalue);
                else
                    ChatGui.Print("[umadenc] 用法: /umadenc kaeshi 0|1|2|3|4");
                break;
            case "st":
                if (tokens.Length >= 2 && int.TryParse(tokens[1], out var sid))
                    UmadDiagnostics.ApplyStatusProbe(sid);
                else
                    ChatGui.Print("[umadenc] 用法: /umadenc st <状态ID>");
                break;
            case "find":
                if (tokens.Length >= 2)
                    UmadDiagnostics.FindStatus(args.Trim().Split(' ', 2)[1]);
                else
                    ChatGui.Print("[umadenc] 用法: /umadenc find <名字片段>");
                break;
            default:
                ChatGui.Print("[UmadChain] 用法: /umadenc [ui | start | p1..p5] [full|solo] [mt..d4|auto] [cd=秒] | role <位置> | cd <秒> | stop");
                break;
        }
    }

    // "p1 100" starts the P1 timeline at its 100th second; anything non-numeric means 0.
    private static float ParseSeconds(string[] tokens)
    {
        foreach (var token in tokens)
            if (float.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                return seconds;
        return 0f;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim())
        {
            case "config":
                ConfigWindow.Toggle();
                break;
            case "mp":
            case "multiplayer":
                MainWindow.OpenMultiplayer();
                break;
            case "start":
                Runs.Start(MainWindow.Selection, solo: false);
                break;
            case "start solo":
                Runs.Start(MainWindow.Selection, solo: true);
                break;
            case "reset":
                Runs.Reset();
                break;
            case "leave":
                Runs.Leave();
                break;
            default:
                MainWindow.Toggle();
                break;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    public void OpenSettings()
    {
        ConfigWindow.IsOpen = true;
        ConfigWindow.BringToFront();
    }
}