using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.ContextMenu;
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

[TweakVersion(2)]
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
        AddChangelogNewTweak("1.8.5.0");
        base.Setup();
    }
    
    public override void LanguageChanged() {
        lockText = new SeStringBuilder().AddUiForeground($"{(char)SeIconChar.ServerTimeEn} ", 500).AddText(LocString("Lock Window Position")).BuiltString;
        unlockText = new SeStringBuilder().AddUiForeground($"{(char)SeIconChar.ServerTimeEn} ", 500).AddText(LocString("Unlock Window Position")).BuiltString;
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        
        moveAddonHook ??= Common.Hook<MoveAddon>("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??", MoveAddonDetour);
        moveAddonHook?.Enable();

        windowClickedHook ??= Common.Hook<WindowClicked>("E8 ?? ?? ?? ?? 84 C0 74 14 48 8B 07 48 8B D6", WindowClickedDetour);
        windowClickedHook?.Enable();
        
        LanguageChanged();
        
        Common.ContextMenu.OnOpenGameObjectContextMenu += ContextMenuOnOnOpenGameObjectContextMenu;
        base.Enable();
    }

    private void ContextMenuOnOnOpenGameObjectContextMenu(GameObjectContextMenuOpenArgs args) {
        if (string.IsNullOrWhiteSpace(args.ParentAddonName)) return;
        if (args.ObjectId != 0xE0000000) return;
        
        var b = *(byte*)((ulong)AgentContext.Instance() + 0x68C);
        if (b != 2) return;
        var str = (Config.LockedWindows.Contains(activeWindowName) ? unlockText : lockText);
        
        args.AddCustomItem(new GameObjectContextMenuItem(str, (_) => {
            if (!Config.LockedWindows.Remove(activeWindowName)) {
                Config.LockedWindows.Add(activeWindowName);
            }
        }));
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

    protected override void Disable() {
        Common.ContextMenu.OnOpenGameObjectContextMenu -= ContextMenuOnOnOpenGameObjectContextMenu;
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
