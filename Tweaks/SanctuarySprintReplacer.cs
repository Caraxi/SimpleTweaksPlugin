using System.Runtime.InteropServices;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

internal unsafe class SanctuarySprintReplacer : Tweak {
    // A huge thank you to Pohky who provided the framework and the base code for this tweak.

    public override string Name => "Island Sanctuary Sprint Replacer";
    public override string Description => "Replaces the normal Sprint action with Isle Sprint while in the Island Sanctuary.";
    protected override string Author => "KazWolfe";
    public override bool Experimental => true;

    private const string GetDutyActionIdSignature = "E8 ?? ?? ?? ?? EB 17 33 C9";

    private delegate uint GetDutyActionId(ushort dutyActionSlot);
    private GetDutyActionId? _getDutyActionId;
    
    private delegate void UseActionDelegate(ActionManager* mgr, ActionType actionType, uint actionID, long targetID, uint a4, uint a5, uint a6, void* a7);
    private HookWrapper<UseActionDelegate>? _useActionHook;

    public override void Enable() {
        this._useActionHook ??= Common.Hook<UseActionDelegate>(ActionManager.fpUseAction, this.UseActionDetour);
        this._useActionHook.Enable();

        if (this._getDutyActionId == null &&
            Service.SigScanner.TryScanText(GetDutyActionIdSignature, out var ptr)) {
            this._getDutyActionId = Marshal.GetDelegateForFunctionPointer<GetDutyActionId>(ptr);
        }

        base.Enable();
    }
        
    public override void Disable() {
        this._useActionHook?.Disable();
        
        base.Disable();
    }

    public override void Dispose() {
        this._useActionHook?.Dispose();
        
        base.Dispose();
    }

    private void UseActionDetour(ActionManager* mgr, ActionType type, uint id, long targetid, uint a4, uint a5, uint a6, void* a7) {
        if (this._getDutyActionId == null) {
            this._useActionHook!.Original(mgr, type, id, targetid, a4, a5, a6, a7);
            return;
        }

        if (AgentMap.Instance()->CurrentTerritoryId != 1055) {
            this._useActionHook!.Original(mgr, type, id, targetid, a4, a5, a6, a7);
            return;
        }
        
        // Override sprint
        if (type == ActionType.General && id == 4) {
            if (this._getDutyActionId(0) == 31314) {
                id = 31314;
                type = ActionType.Spell;
            } else {
                PluginLog.Debug("Got a sprint in Island Sanctuary, but the Sprint DutyAction is not ready.");
            }
        }


        this._useActionHook!.Original(mgr, type, id, targetid, a4, a5, a6, a7);
    }
}