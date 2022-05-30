using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat; 

public unsafe class HideChatPanelButtons : ChatTweaks.SubTweak {
    public override string Name => "Hide Chat Panel Buttons";
    public override string Description => "Hide the chat log name and close button on panels that have been split from the main window.";

    private readonly string[] panels = { "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" };
    
    public override void Enable() {
        Service.ClientState.TerritoryChanged += TerritoryChange;
        ToggleButtons(false);
        base.Enable();
    }

    private void TerritoryChange(object _, ushort territory) => ToggleButtons(false);

    private void ToggleButtons(AtkUnitBase* atkUnitBase, bool visible) {
        if (atkUnitBase == null) return;
        for (var i = 4U; i < 8; i++) {
            var n = atkUnitBase->GetNodeById(i);
            if (n != null) n->ToggleVisibility(visible);
        }
    }

    private void ToggleButtons(bool visible) {
        foreach (var a in panels) {
            var addon = Common.GetUnitBase(a);
            if (addon != null) ToggleButtons(addon, visible);
        }
    }

    public override void Disable() {
        Service.ClientState.TerritoryChanged -= TerritoryChange;
        ToggleButtons(true);
        base.Disable();
    }
}
