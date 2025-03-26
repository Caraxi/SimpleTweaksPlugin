using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Island Sanctuary Sprint Replacer")]
[TweakDescription("Replaces the normal Sprint action with Isle Sprint while in the Island Sanctuary.")]
[TweakAuthor("KazWolfe")]
internal unsafe class SanctuarySprintReplacer : Tweak {
    [TweakHook(typeof(ActionManager), nameof(ActionManager.UseAction), nameof(UseActionDetour))]
    private HookWrapper<ActionManager.Delegates.UseAction> useActionHook;

    [TerritoryChanged]
    public void OnTerritoryChanged(ushort territoryId) {
        useActionHook?.Disable();
        if (territoryId == 1055) useActionHook?.Enable();
    }

    private bool UseActionDetour(ActionManager* mgr, ActionType type, uint id, ulong targetId, uint a4, ActionManager.UseActionMode a5, uint a6, bool* a7) {
        if (AgentMap.Instance()->CurrentTerritoryId != 1055) {
            useActionHook?.Disable();
            return useActionHook!.Original(mgr, type, id, targetId, a4, a5, a6, a7);
        }

        // Override sprint
        if (type == ActionType.GeneralAction && id == 4) {
            if (DutyActionManager.GetDutyActionId(0) == 31314) {
                id = 31314;
                type = ActionType.Action;
            } else {
                SimpleLog.Debug("Got a sprint in Island Sanctuary, but the Sprint DutyAction is not ready.");
            }
        }

        return useActionHook!.Original(mgr, type, id, targetId, a4, a5, a6, a7);
    }
}
