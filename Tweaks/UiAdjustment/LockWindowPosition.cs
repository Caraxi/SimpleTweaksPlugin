using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakVersion(2)]
[TweakName("Lock Window Positions")]
[TweakDescription("Allows locking the position of almost any UI window.")]
[TweakReleaseVersion("1.8.5.0")]
[TweakAutoConfig]
public unsafe class LockWindowPosition : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public HashSet<string> LockedWindows = [];
    }

    private delegate void* MoveAddon(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk);

    [TweakHook, Signature("40 53 48 83 EC 20 80 A2", DetourName = nameof(MoveAddonDetour))]
    private HookWrapper<MoveAddon> moveAddonHook;

    public Configs Config { get; private set; }
    private string addLockInputText = string.Empty;

    protected void DrawConfig(ref bool hasChanged) {
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
    }

    public override void LanguageChanged() {
        lockText = new SeStringBuilder().AddText(LocString("Lock Window Position")).BuiltString;
        unlockText = new SeStringBuilder().AddText(LocString("Unlock Window Position")).BuiltString;
    }

    protected override void Enable() {
        LanguageChanged();
        Service.ContextMenu.OnMenuOpened += OpenContextMenu;
    }

    private void OpenContextMenu(IMenuOpenedArgs args) {
        if (args is not { MenuType: ContextMenuType.Default }) return;
        if (args.Target is not MenuTargetDefault mtd) return;
        if (string.IsNullOrWhiteSpace(args.AddonName)) return;
        if (mtd.TargetObjectId != 0xE0000000) return;
        
        var unitManagerEventInterface = (nint)(&RaptureAtkUnitManager.Instance()->WindowContextMenuHandler);
        if (args.EventInterfaces.All(e => e != unitManagerEventInterface)) return;
        
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
    }
}
