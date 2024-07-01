using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakVersion(2)]
[TweakName("Lock Window Positions")]
[TweakDescription("Allows locking the position of almost any UI window.")]
public unsafe class LockWindowPosition : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public HashSet<string> LockedWindows = new();
    }
    
    private delegate void* MoveAddon(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk);
    private HookWrapper<MoveAddon> moveAddonHook;

    public Configs Config { get; private set; }
    private string addLockInputText = string.Empty;
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {

        ImGui.Text("Manage Locked Windows");
        ImGui.TextDisabled("You can also manage locked windows through their context menus.");
        ImGui.Indent();
        
        hasChanged |= Config.LockedWindows.RemoveWhere((lockedWindow) => {
            var remove = ImGuiComponents.IconButton($"##{lockedWindow}", FontAwesomeIcon.Trash);
            ImGui.SameLine();
            
            if (lockedWindow == addLockInputText.Trim()) {
                ImGuiExt.ShadowedText($"{lockedWindow}", shadowColour: new Vector4(1, 0, 0, 0.75f));
            } else {
                ImGui.Text($"{lockedWindow}");
            }
            
            return remove;
        }) > 0;

        var alreadyExists = string.IsNullOrWhiteSpace(addLockInputText) || Config.LockedWindows.Contains(addLockInputText.Trim());
        
        if (ImGuiComponents.IconButton("##lock", FontAwesomeIcon.Plus, alreadyExists ? ImGuiColors.DalamudGrey3 : null, alreadyExists ? ImGuiColors.DalamudGrey3 : null, alreadyExists ? ImGuiColors.DalamudGrey3 : null)) {
            if (!alreadyExists) {
                Config.LockedWindows.Add(addLockInputText.Trim());
                addLockInputText = string.Empty;
                hasChanged = true;
            }
        }
        ImGui.SameLine();
        ImGui.InputText("Add Window", ref addLockInputText, 0x20);
        ImGui.Unindent();
    };

    public override void Setup() {
        AddChangelogNewTweak("1.8.5.0");
        base.Setup();
    }
    
    public override void LanguageChanged() {
        lockText = new SeStringBuilder().AddText(LocString("Lock Window Position")).BuiltString;
        unlockText = new SeStringBuilder().AddText(LocString("Unlock Window Position")).BuiltString;
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        moveAddonHook ??= Common.Hook<MoveAddon>("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??", MoveAddonDetour);
        moveAddonHook?.Enable();
        LanguageChanged();
        Service.ContextMenu.OnMenuOpened += OpenContextMenu;
        base.Enable();
    }

    private void OpenContextMenu(IMenuOpenedArgs args) {
        if (args is not { MenuType: ContextMenuType.Default }) return;
        if (args.Target is not MenuTargetDefault mtd) return;
        if (string.IsNullOrWhiteSpace(args.AddonName)) return;
        if (mtd.TargetObjectId != 0xE0000000) return;
        
        // TODO: Remove Reflection when fixed, Use field when merged.
        IReadOnlySet<nint>? eventInterfaces = typeof(IMenuArgs).GetField("eventInterfaces", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(args) as IReadOnlySet<nint>;
        var unitManagerEventInterface = (nint)((ulong)RaptureAtkUnitManager.Instance() + 0x9C90); // (nint)(&RaptureAtkUnitManager.Instance()->AtkEventInterface);
        if (eventInterfaces == null || eventInterfaces.All(e => e != unitManagerEventInterface)) return;
        
        var str = (Config.LockedWindows.Contains(args.AddonName) ? unlockText : lockText);
        args.AddMenuItem(new MenuItem() {
            Name = str,
            Prefix = SeIconChar.ServerTimeEn,
            PrefixColor = 500,
            OnClicked = (_) => {
                if (!Config.LockedWindows.Remove(args.AddonName)) {
                    Config.LockedWindows.Add(args.AddonName);
                }
            }
        });
    }

    private void* MoveAddonDetour(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk) {
        try {
            var name = addon->NameString;
            return Config.LockedWindows.Contains(name) ? null : moveAddonHook.Original(atkModule, addon, idk);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
            return moveAddonHook.Original(atkModule, addon, idk);
        }
    }
    
    private SeString lockText = SeString.Empty;
    private SeString unlockText = SeString.Empty;

    protected override void Disable() {
        Service.ContextMenu.OnMenuOpened -= OpenContextMenu;
        moveAddonHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        moveAddonHook?.Dispose();
        base.Dispose();
    }
}
