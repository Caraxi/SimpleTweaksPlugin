using System;
using Dalamud.Game.ClientState.Keys;
using Lumina.Data;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public class ViewOnEriones : ItemHotkey {
    public override string Name => "View On Eriones (JP)";
    
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.SHIFT, VirtualKey.E};

    public override void OnTriggered(ExtendedItem item) {
        var jpItem = Service.Data.Excel.GetSheet<ExtendedItem>(Language.Japanese)?.GetRow(item.RowId);
        if (jpItem == null) return;
        var name = Uri.EscapeUriString(jpItem.Name);
        Common.OpenBrowser($"https://eriones.com/search?i={name}");
    }
}

