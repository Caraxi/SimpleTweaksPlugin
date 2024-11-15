using System;
using Dalamud.Game.ClientState.Keys;
using Lumina.Data;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnConsoleGamesWiki : ItemHotkey {
    protected override string Name => "View On Console Games Wiki";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.W];

    public override bool AcceptsEventItem => false;

    public override void OnTriggered(Item item) {
        if (!Service.Data.Excel.GetSheet<Item>(Language.English).TryGetRow(item.RowId, out var enItem)) return;
        var name = Uri.EscapeDataString(enItem.Name.ExtractText());
        Common.OpenBrowser($"https://ffxiv.consolegameswiki.com/mediawiki/index.php?search={name}");
    }
}
