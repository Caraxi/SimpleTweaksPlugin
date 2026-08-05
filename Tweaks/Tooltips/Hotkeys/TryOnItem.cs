using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public class TryOnItem : ItemHotkey
{
    protected override string Name => "Try On Item";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.F];

    public override void OnTriggered(Item item)
    {
        if (CheckCanTryOn(item))
        {
            AgentTryon.TryOn(0, item.RowId);
        }
    }

    unsafe private static bool CheckCanTryOn(Item item) {
        // not equippable, Waist or SoulCrystal => false
        if (item.EquipSlotCategory.RowId is 0 or 6 or 17)
            return false;

        // any OffHand that's not a Shield => false
        if (item.EquipSlotCategory.RowId is 2 && item.FilterGroup != 3) // 3 = Shield
            return false;

        var race = (int)PlayerState.Instance()->Race;
        if (race == 0)
            return false;

        return true;
    }
}
