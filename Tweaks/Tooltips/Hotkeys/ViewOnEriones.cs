using System;
using Dalamud.Game.ClientState.Keys;
using Lumina.Data;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnEriones : ItemHotkey {
    protected override string Name => "View On Eriones (JP)";

    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.SHIFT, VirtualKey.E };

    public override void OnTriggered(Item item) {
        if (!Service.Data.Excel.GetSheet<Item>(Language.Japanese).TryGetRow(item.RowId, out var jpItem)) return;
        var name = Uri.EscapeDataString(jpItem.Name.ExtractText());
        Common.OpenBrowser($"https://eriones.com/search?i={name}");
    }
}
