using Dalamud.Game.ClientState.Keys;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public class ViewOnGarlandTools : ItemHotkey {
    public override string Name => "View on Garland Tools";
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.CONTROL, VirtualKey.G};
    
    public override void OnTriggered(ExtendedItem item) {
        Common.OpenBrowser($"https://www.garlandtools.org/db/#item/{item.RowId}");
    }
}

