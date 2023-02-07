using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class KeepOpen : Tweak {
    public override string Name => "Keep Windows Open";
    public override string Description => "Prevents certain windows from hiding under specific circumstances.";

    private delegate void* HideUnitBase(AtkUnitBase* atkUnitBase, byte a2, byte a3, int a4);
    private HookWrapper<HideUnitBase> hideHook;


    public class Configs : TweakConfig {
        public SortedSet<string> EnabledWindows = new();
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {

        
        ImGui.TextWrapped("Windows to keep open when accessing retainers, shops or market board:");
        foreach (var (internalName, displayName) in Windows) {
            var enabled = Config.EnabledWindows.Contains(internalName);

            if (ImGui.Checkbox($"{displayName}###keepOpenWindow_{internalName}", ref enabled)) {
                if (enabled) {
                    Config.EnabledWindows.Add(internalName);
                } else {
                    Config.EnabledWindows.Remove(internalName);
                }

                hasChanged = true;
            }
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        hideHook ??= Common.Hook<HideUnitBase>("E8 ?? ?? ?? ?? 32 DB 0F BE D3", HideDetour);
        hideHook?.Enable();
        base.Enable();
    }

    private Dictionary<string, string> Windows = new() {
        ["ItemFinder"] = "Item Search List",
        ["RecipeNote"] = "Crafting Log",
        ["RecipeTree"] = "Recipe Tree",
        ["ContentsInfoDetail"] = "Timer Details",
        ["ContentsInfo"] = "Timers",
    };
    
    private void* HideDetour(AtkUnitBase* atkUnitBase, byte a2, byte a3, int a4) {
        if (atkUnitBase != null && a2 == 1 && a3 == 0 && a4 == 2) {
            var name = Common.ReadString(atkUnitBase->Name, 0x20);
            
            if (Windows.ContainsKey(name)) {
                if (Config.EnabledWindows.Contains(name)) {
                    SimpleLog.Log($"Suppress Hide: {name}");
                    return null;
                }
            } else {
                SimpleLog.Verbose($"Allowed Hiding: {name}");
            }
        }

        return hideHook.Original(atkUnitBase, a2, a3, a4);
    }

    public override void Disable() {
        hideHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        hideHook?.Dispose();
        base.Dispose();
    }
}

