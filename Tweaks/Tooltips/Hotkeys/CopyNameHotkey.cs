using Dalamud.Game.ClientState.Keys;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public class CopyNameHotkey : ItemHotkey {
    public override string Name => "Copy Item Name";
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.CONTROL, VirtualKey.C };

    public override bool AcceptsEventItem => true;
    
    public override void OnTriggered(ExtendedItem item) {
        ImGui.SetClipboardText(item.Name.ToDalamudString().TextValue);
    }

    public override void OnTriggered(EventItem item) {
        ImGui.SetClipboardText(item.Name.ToDalamudString().TextValue);
    }
    
}
