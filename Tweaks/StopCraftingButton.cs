using System.Runtime.InteropServices;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using SimpleTweaksPlugin.TweakSystem;
using XivCommon.Functions.ContextMenu;

namespace SimpleTweaksPlugin.Tweaks; 

// E8 ?? ?? ?? ?? 48 8B 4B 10 33 FF C6 83

public unsafe class StopCraftingButton : Tweak {
    public override string Name => "Stop Crafting Button";
    public override string Description => "Adds an option to stop crafting to the Crafting Log context menu, allowing you to stop crafting without closing the crafting log.";

    private delegate byte EventFunction(EventFramework* eventFramework, uint a2, uint a3, uint a4);
    
    private EventFunction eventFunction;
    private CraftingState* craftingState;
    
    public override void Enable() {
        eventFunction ??= Marshal.GetDelegateForFunctionPointer<EventFunction>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 33 C0 48 8B CB 66 89 83"));
        craftingState = (CraftingState*) Service.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 4B 10 33 FF");
        Plugin.XivCommon.Functions.ContextMenu.OpenContextMenu += ContextMenuOnContextMenuOpened;
        base.Enable();
    }

    public override void Disable() {
        Plugin.XivCommon.Functions.ContextMenu.OpenContextMenu -= ContextMenuOnContextMenuOpened;
        base.Disable();
    }


    [StructLayout(LayoutKind.Explicit)]
    private struct CraftingState {
        [FieldOffset(0x144)] public ushort Unknown;
    }
    
    private void ContextMenuOnContextMenuOpened(ContextMenuOpenArgs args) {
        if (Service.ClientState.LocalPlayer == null) return;
        var localPlayer = (Character*) Service.ClientState.LocalPlayer.Address;
        if (localPlayer->EventState != 5) return;
        if (args.ParentAddonName != "RecipeNote") return;
        args.Items.Add(new NormalContextMenuItem(new SeString(new UIForegroundPayload(539), new TextPayload($"{(char) SeIconChar.ServerTimeEn}"), new UIForegroundPayload(0), new TextPayload(" Stop Crafting")), StopCrafting));
    }
    
    private void StopCrafting(ContextMenuItemSelectedArgs args) {
        if (Service.ClientState.LocalPlayer == null) return;
        var localPlayer = (Character*) Service.ClientState.LocalPlayer.Address;
        if (localPlayer->EventState != 5) return;
        eventFunction(EventFramework.Instance(), 6, 0, 0);
        craftingState->Unknown = 0;
    }
}

