using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Implementations;
using AnoMech.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Scenarios.Umad.FullEncounter;

/// <summary>
/// 绝妖星（UMAD）P1–P5 全流程串联控制器：
/// - 统一提供全局标点（默认国服 Big Box）、开怪倒数、玩家职能、单人/满编、P3/P4 小抄开关及 P3/P5 结束时间。
/// - P2→P3 跳过过场动画时，按实际跳过秒数自动补偿玩家所有冷却中技能的剩余冷却时间。
/// - 进入每一 P 开头时自动存档（资源/Buff/CD），P2→P3 会在补偿完 CD 后再存档，读档时跳过重复补偿与覆盖存档。
/// </summary>
public sealed class UmadEncounterChain : IDisposable
{
    public sealed record StageSpec(
        int PhaseNumber,
        string Label,
        string[] CandidateScenarioNames,
        float TargetUntargetableWindowSec,
        float DefaultBuiltInTailSec);

    public const float ScenarioSettleSec = 1.00f;
    public const float RealP2ToP3UntargetableSec = 46.06f;
    public const float P3PreTargetableSec = 0.80f;
    public const float P3BigBangCombatSec = 282.95f;
    private const int RecastGroupCount = 80;

    public static readonly StageSpec[] Stages =
    [
        new(1, "P1 凯夫卡",   ["P1 Full"],                                       10.35f,  9.35f),
        new(2, "P2 神凯夫卡", ["P2 Full"],       17.50f, 16.50f),
        new(3, "P3 双神",     ["P3 Full"],                       4.46f,  0.00f),
        new(4, "P4 凯夫卡",   ["P4 Full"],                      30.99f, 29.99f),
        new(5, "P5 神凯夫卡", ["P5 Full"],                       0.00f,  0.00f),
    ];

    private static readonly (PartyRole? Role, string ShortName, string DisplayName)[] RoleOptions =
    [
        (null,                    "auto", "自动"),
        (PartyRole.MainTank,      "mt",   "MT"),
        (PartyRole.OffTank,       "st",   "ST"),
        (PartyRole.RegenHealer,   "h1",   "H1"),
        (PartyRole.ShieldHealer,  "h2",   "H2"),
        (PartyRole.MeleeDpsA,     "d1",   "D1"),
        (PartyRole.MeleeDpsB,     "d2",   "D2"),
        (PartyRole.PhysRangedDps, "d3",   "D3"),
        (PartyRole.CasterDps,     "d4",   "D4"),
    ];

    private readonly Game game;
    private readonly Action<string>? notify;
    private readonly float[] builtInTailOverrideSec;
    private readonly EncounterCheckpoint?[] checkpoints;

    private int currentStageIndex = -1;
    private bool active;
    private bool inInstance;

    private int pendingNextIndex = -1;
    private float transitionWaitRemaining;

    public float CountdownSeconds { get; set; } = 5f;
    public PartyRole? SelectedRole { get; set; } = PartyRole.MeleeDpsA;
    public bool SoloMode { get; set; } = false;
    public int SelectedWaymark { get; set; } = ResolveDefaultBigBoxWaymarkIndex();
    public bool EnableP3CheatSheet { get; set; } = true;
    public bool EnableP4CheatSheet { get; set; } = true;
    public float P3ChaosKillDelayAfterBigBangSec { get; set; } = 12.5f;
    public float P3ExdeathKillDelayAfterChaosSec { get; set; } = 6.0f;
    public float P5ClearTimeSec { get; set; } = 205.0f;

    public bool IsActive => active;
    public bool IsWaitingTransition => active && pendingNextIndex >= 0;
    public float TransitionWaitRemaining => transitionWaitRemaining;
    public int CurrentStageIndex => currentStageIndex;
    public int PendingNextStageIndex => pendingNextIndex;

    public UmadEncounterChain(Game game, Action<string>? notify = null)
    {
        this.game = game;
        this.notify = notify;
        builtInTailOverrideSec = Stages.Select(s => s.DefaultBuiltInTailSec).ToArray();
        checkpoints = new EncounterCheckpoint?[Stages.Length];
        this.game.RunFinished += OnRunFinished;
        this.game.InstanceExited += OnInstanceExited;
    }

    private static int ResolveDefaultBigBoxWaymarkIndex()
    {
        var presets = UmadZone.Instance.WaymarkPresets;
        for (var i = 0; i < presets.Count; i++)
        {
            if (presets[i].Name.Contains("Big Box", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    public static IReadOnlyList<(PartyRole? Role, string ShortName, string DisplayName)> GetRoleOptions() => RoleOptions;

    public static IReadOnlyList<WaymarkLayout> GetWaymarkPresets() => UmadZone.Instance.WaymarkPresets;

    public static string FormatWaymarkLabel(WaymarkLayout layout) =>
        layout.Name.Contains("Big Box", StringComparison.OrdinalIgnoreCase)
            ? $"{layout.Name} (国服默认)"
            : layout.Name;

    public EncounterCheckpoint? GetCheckpoint(int stageIndex) =>
        (stageIndex >= 0 && stageIndex < checkpoints.Length) ? checkpoints[stageIndex] : null;

    public bool HasAnyCheckpoint => checkpoints.Any(c => c is not null);

    public void ClearCheckpoints()
    {
        Array.Clear(checkpoints);
        notify?.Invoke("[UmadChain] 已清空全部阶段存档。");
    }

    public void ApplyWaymarkImmediatelyIfInInstance()
    {
        var presets = UmadZone.Instance.WaymarkPresets;
        if (presets.Count == 0) return;
        var idx = Math.Clamp(SelectedWaymark, 0, presets.Count - 1);
        SelectedWaymark = idx;
        if (game.World.Map.IsInInstance)
            game.World.PlaceWaymarks(presets[idx].Markers);
    }

    public float GetBuiltInTailSec(int stageIndex) =>
        (stageIndex >= 0 && stageIndex < builtInTailOverrideSec.Length) ? builtInTailOverrideSec[stageIndex] : 0f;

    public void SetBuiltInTailSec(int stageIndex, float seconds)
    {
        if (stageIndex >= 0 && stageIndex < builtInTailOverrideSec.Length)
            builtInTailOverrideSec[stageIndex] = MathF.Max(0f, seconds);
    }

    public float GetExtraWaitSec(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= Stages.Length) return 0f;
        var target = Stages[stageIndex].TargetUntargetableWindowSec;
        if (target <= 0f) return 0f;
        return MathF.Max(0f, target - builtInTailOverrideSec[stageIndex] - ScenarioSettleSec);
    }

    public float GetP2ToP3SkippedCutsceneSec()
    {
        var actualWait = GetBuiltInTailSec(1) + ScenarioSettleSec + GetExtraWaitSec(1) + P3PreTargetableSec;
        return MathF.Max(0f, RealP2ToP3UntargetableSec - actualWait);
    }

    public IScenario? ResolveScenario(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= Stages.Length) return null;
        foreach (var candidate in Stages[stageIndex].CandidateScenarioNames)
        {
            var found = game.Catalog.Scenarios.FirstOrDefault(s =>
                s.DisplayName().Equals(candidate, StringComparison.Ordinal) ||
                s.Name.Equals(candidate, StringComparison.Ordinal));
            if (found != null) return found;
        }
        return null;
    }

    public bool StartFromPhase(int phaseIndex, bool? soloOverride = null, PartyRole? roleOverride = null, bool applyRoleOverride = false, float? countdownOverride = null)
    {
        phaseIndex = Math.Clamp(phaseIndex, 0, Stages.Length - 1);
        if (soloOverride.HasValue) SoloMode = soloOverride.Value;
        if (applyRoleOverride) SelectedRole = roleOverride;
        if (countdownOverride.HasValue) CountdownSeconds = Math.Clamp(countdownOverride.Value, 0f, 30f);

        var scenario = ResolveScenario(phaseIndex);
        if (scenario == null)
        {
            var expected = string.Join(" / ", Stages[phaseIndex].CandidateScenarioNames);
            notify?.Invoke($"[UmadChain] 未在 ScenarioCatalog 中找到 {Stages[phaseIndex].Label}（期望名称: {expected}）。");
            return false;
        }

        pendingNextIndex = -1;
        transitionWaitRemaining = 0f;
        currentStageIndex = phaseIndex;
        active = true;

        var alreadyInInstance = inInstance || game.ActiveScenario != null;
        LaunchStage(phaseIndex, scenario, enterFresh: !alreadyInInstance, keepPlayerState: alreadyInInstance, isChainedFollowup: false, restoreCheckpoint: null);
        return true;
    }

    public bool LoadCheckpoint(int stageIndex)
    {
        var checkpoint = GetCheckpoint(stageIndex);
        if (checkpoint is null)
        {
            notify?.Invoke($"[UmadChain] 未找到 {Stages[Math.Clamp(stageIndex, 0, Stages.Length - 1)].Label} 的存档。");
            return false;
        }

        var scenario = ResolveScenario(stageIndex);
        if (scenario == null)
        {
            notify?.Invoke($"[UmadChain] 未在 ScenarioCatalog 中找到 {Stages[stageIndex].Label}，无法读档。");
            return false;
        }

        var currentJob = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0u;
        if (checkpoint.ClassJobId != 0 && currentJob != 0 && checkpoint.ClassJobId != currentJob)
            notify?.Invoke("[UmadChain] 提示：当前职业与存档职业不同，已尽量恢复冷却与 Buff。");

        pendingNextIndex = -1;
        transitionWaitRemaining = 0f;
        currentStageIndex = stageIndex;
        active = true;

        var alreadyInInstance = inInstance || game.ActiveScenario != null;
        LaunchStage(stageIndex, scenario, enterFresh: !alreadyInInstance, keepPlayerState: false, isChainedFollowup: true, restoreCheckpoint: checkpoint);
        return true;
    }

    public void Stop()
    {
        active = false;
        pendingNextIndex = -1;
        transitionWaitRemaining = 0f;
        currentStageIndex = -1;
        game.Reset();
        inInstance = game.ActiveScenario != null;
        notify?.Invoke("[UmadChain] 已停止串联并重置场景。");
    }

    public void CancelFromExternal(bool exitedInstance)
    {
        if (exitedInstance) inInstance = false;
        if (!active && pendingNextIndex < 0 && currentStageIndex < 0) return;
        active = false;
        pendingNextIndex = -1;
        transitionWaitRemaining = 0f;
        currentStageIndex = -1;
    }

    private void OnInstanceExited() => CancelFromExternal(exitedInstance: true);

    public void Tick(float deltaSeconds)
    {
        if (!active || pendingNextIndex < 0) return;

        transitionWaitRemaining -= deltaSeconds;
        if (transitionWaitRemaining > 0f) return;

        var nextIndex = pendingNextIndex;
        pendingNextIndex = -1;
        transitionWaitRemaining = 0f;

        var nextScenario = ResolveScenario(nextIndex);
        if (nextScenario == null)
        {
            notify?.Invoke($"[UmadChain] 找不到下一阶段 {Stages[nextIndex].Label}，已停止串联。");
            active = false;
            return;
        }

        currentStageIndex = nextIndex;
        LaunchStage(nextIndex, nextScenario, enterFresh: false, keepPlayerState: true, isChainedFollowup: true, restoreCheckpoint: null);
    }

    public static bool TryParseRole(string token, out PartyRole? role)
    {
        var key = token.Trim().ToLowerInvariant();
        foreach (var opt in RoleOptions)
        {
            if (opt.ShortName == key)
            {
                role = opt.Role;
                return true;
            }
        }
        role = null;
        return false;
    }

    public string FormatRoleLabel()
    {
        foreach (var opt in RoleOptions)
            if (opt.Role == SelectedRole) return opt.DisplayName;
        return SelectedRole?.ToString() ?? "Auto";
    }

    private void OnRunFinished(bool success)
    {
        if (!active) return;

        if (!success)
        {
            notify?.Invoke($"[UmadChain] {Stages[currentStageIndex].Label} 未正常完成（团灭或中断），串联已暂停。");
            active = false;
            pendingNextIndex = -1;
            transitionWaitRemaining = 0f;
            return;
        }

        var nextIndex = currentStageIndex + 1;
        if (nextIndex >= Stages.Length)
        {
            notify?.Invoke("[UmadChain] 绝妖星 P1–P5 全流程已跑完！");
            active = false;
            return;
        }

        var nextScenario = ResolveScenario(nextIndex);
        if (nextScenario == null)
        {
            notify?.Invoke($"[UmadChain] 已完成 {Stages[currentStageIndex].Label}；未注册 {Stages[nextIndex].Label}，串联到此结束。");
            active = false;
            return;
        }

        var extraWait = GetExtraWaitSec(currentStageIndex);
        if (extraWait > 0.01f)
        {
            pendingNextIndex = nextIndex;
            transitionWaitRemaining = extraWait;
            DiagnosticLog.Info($"[UmadChain] P{currentStageIndex + 1}->P{nextIndex + 1} 等待补齐不可选中窗口 {extraWait:F2}s");
            return;
        }

        currentStageIndex = nextIndex;
        LaunchStage(nextIndex, nextScenario, enterFresh: false, keepPlayerState: true, isChainedFollowup: true, restoreCheckpoint: null);
    }

    private void LaunchStage(
        int stageIndex,
        IScenario scenario,
        bool enterFresh,
        bool keepPlayerState,
        bool isChainedFollowup,
        EncounterCheckpoint? restoreCheckpoint)
    {
        var countdownToApply = isChainedFollowup ? 0f : CountdownSeconds;
        ApplyStageOverrides(stageIndex, scenario, countdownToApply, suppressNextCountdown: isChainedFollowup);

        var presets = UmadZone.Instance.WaymarkPresets;
        var waymarkIdx = presets.Count > 0 ? Math.Clamp(SelectedWaymark, 0, presets.Count - 1) : 0;

        var party = new LocalPartyProvider
        {
            RoleOverride = SelectedRole,
            Solo = SoloMode,
        };
        int? ai = (SoloMode || scenario.AiStrats.Count == 0) ? null : 0;

        var p = new RunScenarioParams(
            Scenario: scenario,
            Party: party,
            SelectedAi: ai,
            SelectedWaymark: waymarkIdx,
            Seed: null,
            KeepPlayerState: keepPlayerState);

        void OnStarted(string modeLabel)
        {
            inInstance = true;
            if (restoreCheckpoint is not null)
            {
                restoreCheckpoint.TryRestore(game.Player);
                notify?.Invoke($"[UmadChain] 已读档回到 {Stages[stageIndex].Label} 开头（Buff {restoreCheckpoint.Statuses.Length} / 冷却中 {restoreCheckpoint.ActiveCooldownCount}）。");
            }
            else
            {
                if (isChainedFollowup && stageIndex == 2)
                {
                    var skipped = GetP2ToP3SkippedCutsceneSec();
                    if (skipped > 0.1f)
                        AdvancePlayerCooldowns(skipped);
                }
                checkpoints[stageIndex] = EncounterCheckpoint.CaptureCurrent(stageIndex, Stages[stageIndex].Label, game.Player);
            }
            DiagnosticLog.Info(
                $"[UmadChain] {modeLabel} P{stageIndex + 1} ({scenario.Name}), role={SelectedRole?.ToString() ?? "Auto"}, solo={SoloMode}, cd={countdownToApply:F0}s, waymark={waymarkIdx}, keepState={keepPlayerState}, restore={(restoreCheckpoint is not null)}");
        }

        if (enterFresh)
        {
            ZoneControl.EndSpeedBuffs();
            game.EnterAndStart(p, SessionMode.Local, err =>
            {
                if (!string.IsNullOrEmpty(err))
                {
                    if (err.Contains("already in", StringComparison.OrdinalIgnoreCase))
                    {
                        inInstance = true;
                        var retry = p with { KeepPlayerState = restoreCheckpoint is null };
                        game.StartScenario(retry, retryErr =>
                        {
                            if (!string.IsNullOrEmpty(retryErr))
                            {
                                notify?.Invoke($"[UmadChain] 场内切入 P{stageIndex + 1} 失败: {retryErr}");
                                active = false;
                                return;
                            }
                            OnStarted(restoreCheckpoint is not null ? "读档启动" : "场内启动");
                        });
                        return;
                    }

                    notify?.Invoke($"[UmadChain] 进图启动 P{stageIndex + 1} 失败: {err}");
                    active = false;
                    inInstance = false;
                    return;
                }
                OnStarted(restoreCheckpoint is not null ? "读档进图" : "进图启动");
            });
        }
        else
        {
            game.StartScenario(p, err =>
            {
                if (!string.IsNullOrEmpty(err))
                {
                    notify?.Invoke($"[UmadChain] 衔接 P{stageIndex + 1} 失败: {err}");
                    active = false;
                    return;
                }
                OnStarted(restoreCheckpoint is not null ? "读档重开" : "无缝衔接");
            });
        }
    }

    private void ApplyStageOverrides(int stageIndex, IScenario scenario, float seconds, bool suppressNextCountdown)
    {
        if (scenario.SettingsOverrides is { } ov)
        {
            SetPropertyIfWritable(ov, "CountdownSeconds", seconds);

            if (stageIndex == 2)
            {
                SetPropertyIfWritable(ov, "EnableCheatSheet", EnableP3CheatSheet);
                SetPropertyIfWritable(ov, "ChaosKillDelayAfterBigBangSec", Math.Clamp(P3ChaosKillDelayAfterBigBangSec, 5.0f, 25.0f));
                SetPropertyIfWritable(ov, "ExdeathKillDelayAfterChaosSec", Math.Clamp(P3ExdeathKillDelayAfterChaosSec, 2.0f, 9.5f));
            }
            else if (stageIndex == 3)
            {
                SetPropertyIfWritable(ov, "EnableCheatSheet", EnableP4CheatSheet);
            }
            else if (stageIndex == 4)
            {
                SetPropertyIfWritable(ov, "ClearTimeSec", Math.Clamp(P5ClearTimeSec, 160.0f, 220.0f));
            }
        }

        var suppressField = scenario.GetType().GetField("SuppressNextCountdown");
        if (suppressField != null && suppressField.IsStatic && suppressField.FieldType == typeof(bool))
            suppressField.SetValue(null, suppressNextCountdown);
    }

    private static void SetPropertyIfWritable<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(T))
            prop.SetValue(target, value);
    }

    private static unsafe void AdvancePlayerCooldowns(float seconds)
    {
        if (seconds <= 0f) return;
        var am = ActionManager.Instance();
        if (am == null) return;

        var adjusted = 0;
        for (var i = 0; i < RecastGroupCount; i++)
        {
            var recast = am->GetRecastGroupDetail(i);
            if (recast == null || !recast->IsActive) continue;
            var nextElapsed = recast->Elapsed + seconds;
            if (nextElapsed >= recast->Total)
            {
                recast->IsActive = false;
                recast->Elapsed = 0f;
                recast->Total = 0f;
            }
            else
            {
                recast->Elapsed = nextElapsed;
            }
            adjusted++;
        }

        DiagnosticLog.Info($"[UmadChain] P2->P3 跳过动画补偿冷却 -{seconds:F2}s（共调整 {adjusted} 个冷却组）");
    }

    public void Dispose()
    {
        game.RunFinished -= OnRunFinished;
        game.InstanceExited -= OnInstanceExited;
        active = false;
        pendingNextIndex = -1;
        transitionWaitRemaining = 0f;
        inInstance = false;
    }
}

public sealed class UmadEncounterChainWindow : Window, IDisposable
{
    private readonly UmadEncounterChain chain;
    private int startPhaseIndex;

    public UmadEncounterChainWindow(UmadEncounterChain chain)
        : base("绝妖星全流程串联###UmadEncounterChain")
    {
        this.chain = chain;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(490f, 470f),
            MaximumSize = new Vector2(760f, 820f),
        };
    }

    public override void Draw()
    {
        var roleOptions = UmadEncounterChain.GetRoleOptions();
        var currentRoleIdx = 0;
        for (var i = 0; i < roleOptions.Count; i++)
        {
            if (roleOptions[i].Role == chain.SelectedRole)
            {
                currentRoleIdx = i;
                break;
            }
        }

        const float labelColWidth = 152f;

        ImGui.TextUnformatted("玩家位置");
        ImGui.SameLine(labelColWidth);
        ImGui.SetNextItemWidth(180f);
        if (ImGui.BeginCombo("##umad_role", roleOptions[currentRoleIdx].DisplayName))
        {
            for (var i = 0; i < roleOptions.Count; i++)
            {
                var selected = i == currentRoleIdx;
                if (ImGui.Selectable(roleOptions[i].DisplayName, selected))
                    chain.SelectedRole = roleOptions[i].Role;
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        var cd = chain.CountdownSeconds;
        ImGui.TextUnformatted("开怪倒数");
        ImGui.SameLine(labelColWidth);
        ImGui.SetNextItemWidth(180f);
        if (ImGui.SliderFloat("##umad_cd", ref cd, 0f, 15f, "%.0f s"))
            chain.CountdownSeconds = MathF.Round(cd);

        ImGui.TextUnformatted("起始阶段");
        ImGui.SameLine(labelColWidth);
        ImGui.SetNextItemWidth(180f);
        if (ImGui.BeginCombo("##umad_start_phase", UmadEncounterChain.Stages[startPhaseIndex].Label))
        {
            for (var i = 0; i < UmadEncounterChain.Stages.Length; i++)
            {
                var selected = i == startPhaseIndex;
                if (ImGui.Selectable(UmadEncounterChain.Stages[i].Label, selected))
                    startPhaseIndex = i;
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.TextUnformatted("小抄开关");
        ImGui.SameLine(labelColWidth);
        var p3Cheat = chain.EnableP3CheatSheet;
        if (ImGui.Checkbox("开启 P3 小抄##chain_p3_cheat", ref p3Cheat))
            chain.EnableP3CheatSheet = p3Cheat;
        ImGui.SameLine();
        var p4Cheat = chain.EnableP4CheatSheet;
        if (ImGui.Checkbox("开启 P4 小抄##chain_p4_cheat", ref p4Cheat))
            chain.EnableP4CheatSheet = p4Cheat;

        var chaosDelay = chain.P3ChaosKillDelayAfterBigBangSec;
        ImGui.TextUnformatted("顶起后卡奥斯击杀");
        ImGui.SameLine(labelColWidth);
        ImGui.SetNextItemWidth(180f);
        if (ImGui.SliderFloat("##umad_p3_chaos_kill", ref chaosDelay, 5.0f, 25.0f, "+%.1f s"))
            chain.P3ChaosKillDelayAfterBigBangSec = chaosDelay;

        var exDelay = chain.P3ExdeathKillDelayAfterChaosSec;
        ImGui.TextUnformatted("卡奥斯死后小艾击杀");
        ImGui.SameLine(labelColWidth);
        ImGui.SetNextItemWidth(180f);
        if (ImGui.SliderFloat("##umad_p3_ex_kill", ref exDelay, 2.0f, 9.5f, "+%.1f s"))
            chain.P3ExdeathKillDelayAfterChaosSec = exDelay;

        var p5Clear = chain.P5ClearTimeSec;
        ImGui.TextUnformatted("P5 结束时间");
        ImGui.SameLine(labelColWidth);
        ImGui.SetNextItemWidth(180f);
        if (ImGui.SliderFloat("##umad_p5_clear", ref p5Clear, 160f, 220f, "%.1f 秒"))
            chain.P5ClearTimeSec = p5Clear;

        ImGui.Spacing();
        if (ImGui.Button("开始串联", new Vector2(115f, 0f)))
            chain.StartFromPhase(startPhaseIndex);
        ImGui.SameLine();
        if (ImGui.Button("停止 / 重置", new Vector2(115f, 0f)))
            chain.Stop();
        if (chain.HasAnyCheckpoint)
        {
            ImGui.SameLine();
            if (ImGui.Button("清空存档", new Vector2(96f, 0f)))
                chain.ClearCheckpoints();
        }

        if (chain.IsWaitingTransition)
        {
            ImGui.Spacing();
            ImGui.TextColored(
                new Vector4(1f, 0.85f, 0.3f, 1f),
                $"转场不可选中等待中：即将进入 P{chain.PendingNextStageIndex + 1}（剩余 {chain.TransitionWaitRemaining:F2}s）");
        }
        else if (chain.IsActive && chain.CurrentStageIndex >= 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(
                new Vector4(0.45f, 0.95f, 0.55f, 1f),
                $"运行中：{UmadEncounterChain.Stages[chain.CurrentStageIndex].Label} | 位置：{chain.FormatRoleLabel()}");
        }

        ImGui.Separator();
        ImGui.TextUnformatted("阶段状态、自动存档（资源/Buff/CD）与转场窗口");

        for (var i = 0; i < UmadEncounterChain.Stages.Length; i++)
        {
            var spec = UmadEncounterChain.Stages[i];
            var resolved = chain.ResolveScenario(i);
            var status = resolved != null ? resolved.Name : "未注册";
            var cp = chain.GetCheckpoint(i);

            ImGui.TextUnformatted($"{spec.Label}: {status}");
            if (cp is not null)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"读档##load_cp_{i}"))
                    chain.LoadCheckpoint(i);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"存档时间 {cp.CapturedAt:HH:mm:ss}\nBuff: {cp.Statuses.Length} 个 | 冷却中技能: {cp.ActiveCooldownCount} 个\n点击立即从 {spec.Label} 开头恢复并继续串联");
                ImGui.SameLine();
                ImGui.TextDisabled($"({cp.CapturedAt:HH:mm:ss})");
            }

            if (spec.TargetUntargetableWindowSec > 0f)
            {
                var builtIn = chain.GetBuiltInTailSec(i);
                var extra = chain.GetExtraWaitSec(i);
                var note = i == 1
                    ? $"（跳过动画 {chain.GetP2ToP3SkippedCutsceneSec():F2}s，先补偿冷却再存档）"
                    : string.Empty;
                ImGui.TextUnformatted(
                    $"  P{spec.PhaseNumber}->P{spec.PhaseNumber + 1} 目标 {spec.TargetUntargetableWindowSec:F2}s (尾段 {builtIn:F2}s + 结算 {UmadEncounterChain.ScenarioSettleSec:F2}s + 补齐 {extra:F2}s){note}");
            }
        }
    }

    public void Dispose() { }
}