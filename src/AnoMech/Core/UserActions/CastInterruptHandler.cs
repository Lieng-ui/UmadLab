using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.UserActions;

// The server interrupts a player's cast on move/jump/cancel or when a hostile cast target
// becomes untargetable by echoing an ActorControl(15) back, which the client handles to
// clear the bar. The sim firewall eats that packet, so we synthesize it. No interrupt inside
// the last CastInterruptThreshold seconds (~0.5s = when the server resolves the action):
// that's the slidecast window, where the cast is committed.
internal sealed unsafe class CastInterruptHandler : IUserActionHandler
{
    private readonly ActorControl localPlayer = new(() => BattleCharaProxy.LocalPlayer);
    private uint castAction;
    private ulong castTargetId;
    private bool castTargetWasEnemy;

    public void OnScenarioStart() => ClearTrackedCast();
    public void OnAction(ActionType actionType, uint actionId) => ClearTrackedCast();
    public void OnCastInterrupted(ActionType actionType, uint actionId) => ClearTrackedCast();

    public void OnCastStart(ActionType actionType, uint actionId, ulong targetId)
    {
        ClearTrackedCast();
        if (actionType != ActionType.Action || targetId == 0 || targetId == 0xE0000000UL) return;
        var isEnemy = Plugin.Game?.World.Children.OfType<SimEnemy>()
            .Any(e => (ulong)e.GameObjectId == targetId) ?? false;
        if (!isEnemy) return;
        castAction = actionId;
        castTargetId = targetId;
        castTargetWasEnemy = true;
    }

    public void OnTick(float deltaSeconds)
    {
        var hooks = Natives.PlayerInput;
        var cancelRequested = hooks.PollCancelCast(); // drain every frame so a stale latch can't carry over

        var player = (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);
        if (player == null || !player->CastInfo.IsCasting) return;

        var remaining = player->CastInfo.TotalCastTime - player->CastInfo.CurrentCastTime;
        if (remaining <= Plugin.Config.CastInterruptThreshold) return;

        if (hooks.MovementInputActive || hooks.IsJumping || cancelRequested)
        {
            localPlayer.CancelCast(player->CastInfo.ActionId, CastCancelReason.SelfCancelled);
            ClearTrackedCast();
            return;
        }

        if (EnemyTargetLost(player->CastInfo.ActionId))
        {
            localPlayer.CancelCast(player->CastInfo.ActionId, CastCancelReason.Cancelled);
            ClearTrackedCast();
        }
    }

    private bool EnemyTargetLost(uint activeActionId)
    {
        if (!castTargetWasEnemy || castTargetId == 0 || castAction != activeActionId) return false;
        var enemy = Plugin.Game?.World.Children.OfType<SimEnemy>()
            .FirstOrDefault(e => (ulong)e.GameObjectId == castTargetId);
        if (enemy == null || !enemy.IsActive || !enemy.Targetable) return true;
        var cm = CharacterManager.Instance();
        if (cm == null) return false;
        var bc = cm->LookupBattleCharaByEntityId(enemy.GameObjectId.ObjectId);
        return bc == null || !((GameObject*)bc)->GetIsTargetable();
    }

    private void ClearTrackedCast()
    {
        castAction = 0;
        castTargetId = 0;
        castTargetWasEnemy = false;
    }
}
