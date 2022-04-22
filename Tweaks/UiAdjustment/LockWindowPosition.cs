using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Gui.ContextMenus;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class LockWindowPosition : UiAdjustments.SubTweak {
    public override string Name => "Lock Window Positions";

    public override string Description => "Allows locking the position of almost any UI window.";

    public class Configs : TweakConfig {
        public HashSet<string> LockedWindows = new();
    }

    public Configs Config { get; private set; }
    private delegate void* MoveAddon(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk);
    private HookWrapper<MoveAddon> moveAddonHook;

    public override void LanguageChanged() {
        lockText = new SeString(new TextPayload(LocString("Lock Window Position")));
        unlockText = new SeString(new TextPayload(LocString("Unlock Window Position")));
    }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        moveAddonHook ??= Common.Hook<MoveAddon>("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??", MoveAddonDetour);
        moveAddonHook?.Enable();

        Service.ContextMenu.ContextMenuOpened += ContextMenuOnOpenContextMenu;
        defaultText = Service.Data.Excel.GetSheet<Addon>()?.GetRow(8660)?.Text?.RawString ?? "Return to Default Position";

        LanguageChanged();

        base.Enable();
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

        ImGui.TextWrapped(LocString("HelpMessage", "You can lock or unlock windows from their context menu (right click the window)."));

        string unlock = null;
        foreach (var l in Config.LockedWindows) {
            if (ImGui.Button(LocString("Unlock") + "##{l}")) {
                unlock = l;
            }
            ImGui.SameLine();
            ImGui.Text($"{l}");
        }
        if (unlock != null) Config.LockedWindows.Remove(unlock);
    };

    private void* MoveAddonDetour(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk) {
        var name = Marshal.PtrToStringUTF8(new IntPtr(addon->Name));
        return Config.LockedWindows.Contains(name) ? null : moveAddonHook.Original(atkModule, addon, idk);
    }

    private SeString lockText = SeString.Empty;
    private SeString unlockText = SeString.Empty;
    private string defaultText = string.Empty;

    
    private void ContextMenuOnOpenContextMenu(ContextMenuOpenedArgs args) {
        if (args.ParentAddonName == null) return;
        
        var items = args.GetItems();
        if (items.Any(i => i.Name.TextValue == defaultText)) {
            args.AddSimpleItem(Config.LockedWindows.Contains(args.ParentAddonName) ? unlockText : lockText, ToggleWindowPositionLock);
        }
    }

    private void ToggleWindowPositionLock(CustomContextMenuItemSelectedArgs args) {
        if (!Enabled) return;
        if (args.ContextMenuOpenedArgs.ParentAddonName == null) return;
        if (string.IsNullOrWhiteSpace(args.ContextMenuOpenedArgs.ParentAddonName)) return;

        if (Config.LockedWindows.Contains(args.ContextMenuOpenedArgs.ParentAddonName))
            Config.LockedWindows.Remove(args.ContextMenuOpenedArgs.ParentAddonName);
        else
            Config.LockedWindows.Add(args.ContextMenuOpenedArgs.ParentAddonName);
    }

    public override void Disable() {
        moveAddonHook?.Disable();
        SaveConfig(Config);
        Service.ContextMenu.ContextMenuOpened -= ContextMenuOnOpenContextMenu;
        base.Disable();
    }

    public override void Dispose() {
        moveAddonHook?.Dispose();
        base.Dispose();
    }
}