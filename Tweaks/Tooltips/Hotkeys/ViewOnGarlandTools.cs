using Dalamud.Game.ClientState.Keys;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class ViewOnGarlandTools : ItemHotkey {
    protected override string Name => "View on Garland Tools";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.G];

    public override void OnTriggered(Item item) {
        Common.OpenBrowser($"https://www.garlandtools.org/db/#item/{item.RowId}");
    }
}
