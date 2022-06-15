using Dalamud.Game.ClientState.Keys;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public class ViewOnUniversalis : ItemHotkey {
    public override string Name => "View on Universalis";
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.CONTROL, VirtualKey.U};

    public override void OnTriggered(ExtendedItem item) {
        Common.OpenBrowser($"https://universalis.app/market/{item.RowId}");
    }

    public override bool DoShow(ExtendedItem item) {
        return item.ItemSearchCategory.Row != 0;
    }
}

