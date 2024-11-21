using Dalamud.Game.ClientState.Keys;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnUniversalis : ItemHotkey {
    protected override string Name => "View on Universalis";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.U];

    public override void OnTriggered(Item item) {
        Common.OpenBrowser($"https://universalis.app/market/{item.RowId}");
    }

    public override bool DoShow(Item item) {
        return item.ItemSearchCategory.RowId != 0;
    }
}
