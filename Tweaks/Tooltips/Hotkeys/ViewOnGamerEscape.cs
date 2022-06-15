using System;
using Dalamud.Game.ClientState.Keys;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public class ViewOnGamerEscape : ItemHotkey {
    public override string Name => "View On Gamer Escape";
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.CONTROL, VirtualKey.E};

    public override bool AcceptsEventItem => true;

    public override void OnTriggered(ExtendedItem item) {
        var enItem = Service.Data.Excel.GetSheet<ExtendedItem>(Language.English)?.GetRow(item.RowId);
        if (enItem == null) return;
        var name = Uri.EscapeUriString(enItem.Name);
        Common.OpenBrowser($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
    }
    
    public override void OnTriggered(EventItem item) {
        var enItem = Service.Data.Excel.GetSheet<ExtendedItem>(Language.English)?.GetRow(item.RowId);
        if (enItem == null) return;
        var name = Uri.EscapeUriString(enItem.Name);
        Common.OpenBrowser($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
    }
}

