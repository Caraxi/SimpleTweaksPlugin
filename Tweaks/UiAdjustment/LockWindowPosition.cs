using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class LockWindowPosition : UiAdjustments.SubTweak {
    public override string Name => "Lock Window Positions";
    public override string Description => "Allows locking the position of almost any UI window.";

    public class Configs : TweakConfig {
        public HashSet<string> LockedWindows = new();
    }
    
    private delegate void* MoveAddon(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk);
    private HookWrapper<MoveAddon> moveAddonHook;

    private delegate void* AddMenuItem(AgentContext* agent, uint addonTextId, void* a3, long a4, byte a5, void* a6, void* a7, void* a8, void* a9, void* a10);
    private HookWrapper<AddMenuItem> addMenuItemHook;

    private delegate byte WindowClicked(AtkUnitBase* unitBase, ushort a2, ushort a3);
    private HookWrapper<WindowClicked> windowClickedHook;

    private delegate void* WindowContextHandle(void* a1, void* a2, void* a3, void* a4, uint a5);
    private HookWrapper<WindowContextHandle> windowContextHandleHook;

    public Configs Config { get; private set; }
    private string addLockInputText = string.Empty;

    private string activeWindowName = string.Empty;
    
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
        AddChangelogNewTweak(Changelog.UnreleasedVersion);
        base.Setup();
    }
    
    public override void LanguageChanged() {
        lockText = new SeStringBuilder().AddUiForeground($"{(char)SeIconChar.ServerTimeEn} ", 500).AddText(LocString("Lock Window Position")).BuiltString;
        unlockText = new SeStringBuilder().AddUiForeground($"{(char)SeIconChar.ServerTimeEn} ", 500).AddText(LocString("Unlock Window Position")).BuiltString;
    }
    
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        
        moveAddonHook ??= Common.Hook<MoveAddon>("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??", MoveAddonDetour);
        moveAddonHook?.Enable();

        addMenuItemHook ??= Common.Hook<AddMenuItem>("E8 ?? ?? ?? ?? 41 8B 57 3C", AddMenuItemDetour);
        addMenuItemHook?.Enable();

        windowClickedHook ??= Common.Hook<WindowClicked>("E8 ?? ?? ?? ?? 84 C0 74 14 48 8B 07 48 8B D6", WindowClickedDetour);
        windowClickedHook?.Enable();

        windowContextHandleHook ??= Common.Hook<WindowContextHandle>("48 89 6C 24 ?? 48 89 54 24 ?? 56 41 54", WindowContextHandleDetour);
        windowContextHandleHook?.Enable();
        
        LanguageChanged();
        
        base.Enable();
    }

    private void* WindowContextHandleDetour(void* a1, void* a2, void* a3, void* a4, uint a5) {
        try {
            if (a5 == ToggleLockContextAction) {
                var agent = AgentContext.Instance();
                var ownerAddon = Common.GetAddonByID(agent->OwnerAddon);
                if (ownerAddon != null) {
                    var name = Common.ReadString(ownerAddon->Name, 0x20);
                    if (!string.IsNullOrWhiteSpace(name)) {
                        if (!Config.LockedWindows.Remove(name)) {
                            Config.LockedWindows.Add(name);
                        }
                    }
                }

                return null;
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        
        return windowContextHandleHook.Original(a1, a2, a3, a4, a5);
    }

    private byte WindowClickedDetour(AtkUnitBase* unitBase, ushort a2, ushort a3) {
        var ret = windowClickedHook.Original(unitBase, a2, a3);
        try {
            if (ret != 0) {
                try {
                    activeWindowName = Common.ReadString(unitBase->Name, 0x20);
                } catch {
                    activeWindowName = string.Empty;
                }
            
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        
        return ret;
    }

    public const uint ToggleLockContextAction = 0x5354 + 1;
    
    private void* AddMenuItemDetour(AgentContext* agent, uint addonTextId, void* a3, long a4, byte a5, void* a6, void* a7, void* a8, void* a9, void* a10) {
        var retVal = addMenuItemHook.Original(agent, addonTextId, a3, a4, a5, a6, a7, a8, a9, a10);
        try {
            if (addonTextId == 8660 && !string.IsNullOrEmpty(activeWindowName)) {
                var str = (Config.LockedWindows.Contains(activeWindowName) ? unlockText : lockText).Encode();
                fixed (byte* ptr = &str[0]) agent->AddMenuItem(ptr, a3, ToggleLockContextAction);
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        
        return retVal;
    }

    private void* MoveAddonDetour(RaptureAtkModule* atkModule, AtkUnitBase* addon, void* idk) {
        try {
            var name = Common.ReadString(addon->Name, 0x20);
            return Config.LockedWindows.Contains(name) ? null : moveAddonHook.Original(atkModule, addon, idk);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
            return moveAddonHook.Original(atkModule, addon, idk);
        }
    }
    
    private SeString lockText = SeString.Empty;
    private SeString unlockText = SeString.Empty;

    public override void Disable() {
        moveAddonHook?.Disable();
        addMenuItemHook?.Disable();
        windowClickedHook?.Disable();
        windowContextHandleHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        moveAddonHook?.Dispose();
        addMenuItemHook?.Dispose();
        windowClickedHook?.Disable();
        windowContextHandleHook?.Disable();
        base.Dispose();
    }
}
