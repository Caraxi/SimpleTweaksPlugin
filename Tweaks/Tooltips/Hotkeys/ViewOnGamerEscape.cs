using System;
using Dalamud.Game.ClientState.Keys;
using Lumina.Data;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnGamerEscape : ItemHotkey {
    protected override string Name => "View On Gamer Escape";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.E];

    public override bool AcceptsEventItem => true;

    public override void OnTriggered(Item item) {
        if (!Service.Data.Excel.GetSheet<Item>(Language.English).TryGetRow(item.RowId, out var enItem)) return;
        var name = Uri.EscapeDataString(enItem.Name.ExtractText());
        Common.OpenBrowser($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
    }

    public override void OnTriggered(EventItem item) {
        if (!Service.Data.Excel.GetSheet<EventItem>(Language.English).TryGetRow(item.RowId, out var enItem)) return;
        var name = Uri.EscapeDataString(enItem.Name.ExtractText());
        Common.OpenBrowser($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
    }
}
