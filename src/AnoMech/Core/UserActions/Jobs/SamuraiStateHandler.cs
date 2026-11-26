using System;
using AnoMech.Core;
using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Samurai's Sen (Setsu/Getsu/Ka bitfield) and Kaeshi (Tsubame-gaeshi mirror) — the non-scalar
// state a plain ResourceGauge can't hold. Sen count drives the Iaijutsu button
// (1→Higanbana, 2→Tenka Goken, 3→Midare Setsugekka) via GetAdjustedActionId; Kaeshi drives the
// Tsubame-gaeshi mirror. Kenki, statuses, and Meditation stay in the JobActions data table.
//
// This is the reference example for a per-job state handler: gate on job, switch on the executed
// action id, write the gauge struct through a JobGaugeEdit, and combo-gate anything the sheet marks a combo
// bonus (read PRE-advance combo via PlayerCombo — so state handlers run before ActionEffectHandler).
internal sealed unsafe class SamuraiStateHandler : IUserActionHandler
{
    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        if (PlayerJob.Current != JobId.Samurai) return;

        var player = Plugin.Game?.Player;
        bool meikyo = player?.HasStatus(1233) == true;   // Meikyo Shisui, 3 stacks

        bool gainer = actionId is 7480 or 7481 or 7482 or 7484 or 7485;
        bool comboOk = Combo(actionId);
        bool viaMeikyo = gainer && !comboOk && meikyo;   // Meikyo waives the combo requirement

        int before = GetLocalStatusParam(1233);

        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;

        byte kaeshiBefore = (byte)jgm->Samurai.Kaeshi;
        byte senBefore = (byte)jgm->Samurai.SenFlags;

        switch (actionId)
        {
            // Sen generators — on a valid combo finisher, or with Meikyo Shisui up:
            case 7480 when comboOk || viaMeikyo: jgm->Samurai.SenFlags |= SenFlags.Setsu; break;   // Yukikaze
            case 7481 when comboOk || viaMeikyo: jgm->Samurai.SenFlags |= SenFlags.Getsu; break;   // Gekko
            case 7482 when comboOk || viaMeikyo: jgm->Samurai.SenFlags |= SenFlags.Ka;    break;   // Kasha
            case 7484 when comboOk || viaMeikyo: jgm->Samurai.SenFlags |= SenFlags.Getsu; break;   // Mangetsu (AoE)
            case 7485 when comboOk || viaMeikyo: jgm->Samurai.SenFlags |= SenFlags.Ka;    break;   // Oka (AoE)
            // Iaijutsu — arm the Kaeshi mirror and spend the Sen. The icon the client shows comes
            // from the "Kaeshi ready" status JobActionHandler applies per iaijutsu (3852 / 4216 /
            // 4217 / 4218), not from this byte.
            case 7489: jgm->Samurai.Kaeshi = KaeshiAction.Higanbana; jgm->Samurai.SenFlags = SenFlags.None; break;   // Higanbana
            case 7488: jgm->Samurai.Kaeshi = KaeshiAction.Goken;     jgm->Samurai.SenFlags = SenFlags.None; break;   // Tenka Goken
            case 7487: jgm->Samurai.Kaeshi = KaeshiAction.Setsugekka; jgm->Samurai.SenFlags = SenFlags.None; break;  // Midare Setsugekka
            case 36965: jgm->Samurai.Kaeshi = KaeshiAction.Goken;     jgm->Samurai.SenFlags = SenFlags.None; break;  // Tendo Goken
            case 36966: jgm->Samurai.Kaeshi = KaeshiAction.Setsugekka; jgm->Samurai.SenFlags = SenFlags.None; break; // Tendo Setsugekka
            case 25781: jgm->Samurai.Kaeshi = KaeshiAction.Namikiri; break;   // Ogi Namikiri (no Sen)
            case 7495: jgm->Samurai.SenFlags = SenFlags.None; break;          // Hagakure (converts Sen → Kenki)
            // Kaeshi mirrors consume the armed Kaeshi:
            case 16484 or 16485 or 16486 or 25782 or 36967 or 36968:
                jgm->Samurai.Kaeshi = default;
                if (player is { } pl)
                {
                    pl.RemoveStatus(3852);   // Kaeshi: Goken ready
                    pl.RemoveStatus(4216);   // Kaeshi: Setsugekka ready
                    pl.RemoveStatus(4217);   // Tendo Kaeshi: Goken ready
                    pl.RemoveStatus(4218);   // Tendo Kaeshi: Setsugekka ready
                }
                break;
        }

        if (actionId is 7487 or 7488 or 7489 or 36965 or 36966 or 25781)
        {
            DiagnosticLog.Info(
                $"[umadenc kaeshi] action=0x{actionId:X} senBefore={senBefore} " +
                $"kaeshiBefore={kaeshiBefore} kaeshiAfter={(byte)jgm->Samurai.Kaeshi} " +
                $"has3852={player?.HasStatus(3852) == true} has4216={player?.HasStatus(4216) == true} " +
                $"has4218={player?.HasStatus(4218) == true}");
        }

        // Meikyo waives the combo, and the tooltip keeps the buffs it would have granted: Gekko /
        // Mangetsu still apply Fugetsu, Kasha / Oka still apply Fuka. The stack itself is spent by
        // JobActionHandler -> JobActions.ClearStatuses ([1233] = AnyWeaponskill), not here.
        if (viaMeikyo && player is { } p)
        {
            switch (actionId)
            {
                case 7481 or 7484: p.AddStatus(1298, 40f); break;
                case 7482 or 7485: p.AddStatus(1299, 40f); break;
            }

            // Meikyo makes the weaponskill count as a combo finisher, so its Kenki bonus lands too.
            int kenkiGain = actionId == 7480 ? 15 : 10;
            jgm->Samurai.Kenki = (byte)Math.Min(100, jgm->Samurai.Kenki + kenkiGain);
        }

        int after = GetLocalStatusParam(1233);
        if (meikyo || viaMeikyo || gainer || actionId == 7499)
        {
            DiagnosticLog.Info(
                $"[umadenc meikyo] action=0x{actionId:X} combo={comboOk} viaMeikyo={viaMeikyo} " +
                $"stacksBefore={before} stacksAfter={after} call={++meikyoCalls}");
        }
    }

    private static int meikyoCalls;

    private static int GetLocalStatusParam(uint statusId)
    {
        var lp = Plugin.ObjectTable.LocalPlayer;
        if (lp == null) return -1;
        foreach (var s in lp.StatusList)
            if (s.StatusId == statusId)
                return s.Param;
        return 0;
    }

    private static bool Combo(uint actionId) => PlayerCombo.IsActiveContinuation(actionId);
}
