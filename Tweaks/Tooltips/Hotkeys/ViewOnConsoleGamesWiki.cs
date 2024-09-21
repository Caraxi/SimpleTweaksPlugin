using System;
using Dalamud.Game.ClientState.Keys;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnConsoleGamesWiki : ItemHotkey {
    protected override string Name => "View On Console Games Wiki";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.W];

    public override bool AcceptsEventItem => false;

    public override void OnTriggered(ExtendedItem item) {
        var enItem = Service.Data.Excel.GetSheet<ExtendedItem>(Language.English)?.GetRow(item.RowId);
        if (enItem == null) return;
        var name = Uri.EscapeDataString(enItem.Name);
        Common.OpenBrowser($"https://ffxiv.consolegameswiki.com/mediawiki/index.php?search={name}");
    }
}
