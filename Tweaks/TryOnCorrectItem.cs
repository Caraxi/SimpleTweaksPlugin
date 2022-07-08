using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public class TryOnCorrectItem : Tweak {
    public override string Name => "Try On Correct Item";
    public override string Description => "Show the correct item when trying on a glamoured item.";

    private delegate byte TryOn(uint unknownCanEquip, uint itemBaseId, ulong stainColor, uint itemGlamourId, byte unknownByte);
    private HookWrapper<TryOn> tryOnHook;
    private List<string> newWindows = new();
    
    public enum TryOnItem {
        Original,
        Glamoured,
    }
    
    public class TryOnWindowSettings {
        public TryOnItem NoModifier = TryOnItem.Glamoured;
        public TryOnItem HoldingShift = TryOnItem.Original;
    }
    
    public class Configs : TweakConfig {

        #region Old Settings
        // Old Settings TODO: Remove
        public bool ShouldSerializeShiftForUnglamoured => ShiftForUnglamoured != null;
        public bool ShouldSerializeInvertShift => InvertShift != null;
        public bool? ShiftForUnglamoured = null;
        public bool? InvertShift = null;
        #endregion Old Settings
        
        public TryOnWindowSettings Default = new();
        public Dictionary<string, TryOnWindowSettings> WindowSettings = new();
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool changed) => {
        
        if (ImGui.BeginTable("tryOnCorrectItemSettings", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoBordersInBody)) {
            
            
            ImGui.TableSetupColumn("Window", ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("No Modifier", ImGuiTableColumnFlags.WidthFixed, 130 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("Holding Shift", ImGuiTableColumnFlags.WidthFixed, 130 * ImGui.GetIO().FontGlobalScale);
            
            ImGui.TableHeadersRow();

            void Editor(string name, TryOnWindowSettings settings) {
                ImGui.PushID($"tryOnCorrectWindowSettings_{name}");
                ImGui.TableNextColumn();
                ImGui.Text(name);
                ImGui.TableNextColumn();

                void ValueEditor(string key, ref TryOnItem value) {
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo($"##{key}", $"{value}", ImGuiComboFlags.None)) {
                        if (ImGui.Selectable($"{TryOnItem.Original}", value == TryOnItem.Original)) {value = TryOnItem.Original;}
                        if (ImGui.Selectable($"{TryOnItem.Glamoured}", value == TryOnItem.Glamoured)) value = TryOnItem.Glamoured;
                        ImGui.EndCombo();
                    }
                }
                
                ValueEditor("No Modifier", ref settings.NoModifier);
                ImGui.TableNextColumn();
                ValueEditor("Holding Shift", ref settings.HoldingShift);
                
                ImGui.PopID();
            }
            
            Editor("Default", Config.Default);
            foreach (var (name, settings) in Config.WindowSettings) {
                Editor(name, settings);
            }

            
            if (newWindows.Count > 0) {
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##tryOnCorrectItemSettings_newWindow", "Select Window to Add...", ImGuiComboFlags.None)) {
                    foreach (var window in newWindows) {
                        if (ImGui.Selectable(window, false)) {
                            if (!Config.WindowSettings.ContainsKey(window)) {
                                Config.WindowSettings.Add(window, new TryOnWindowSettings() {
                                    HoldingShift = Config.Default.HoldingShift,
                                    NoModifier = Config.Default.NoModifier,
                                });
                            }
                            newWindows.Remove(window);
                            break;
                        }
                    }
                    ImGui.EndCombo();
                }
            }
            
            ImGui.EndTable();
        }


    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        
        
        // Load from old settings
        if (Config.ShiftForUnglamoured != null) {
            if (Config.ShiftForUnglamoured == false) {
                Config.Default.HoldingShift = TryOnItem.Glamoured;
                Config.Default.NoModifier = TryOnItem.Glamoured;
            } else {
                if (Config.InvertShift != null) {
                    if (Config.InvertShift.Value) {
                        Config.Default.HoldingShift = TryOnItem.Glamoured;
                        Config.Default.NoModifier = TryOnItem.Original;
                    }
                }
            }

            Config.ShiftForUnglamoured = null;
            Config.InvertShift = null;
            SaveConfig(Config);
        }
        
        
        
        tryOnHook ??= Common.Hook<TryOn>("E8 ?? ?? ?? ?? EB 35 BA", TryOnDetour);
        tryOnHook?.Enable();
        base.Enable();
    }

    private unsafe TryOnWindowSettings GetActiveWindowSettings() {
        var addonId = 0U;
        var agentContext = AgentContext.Instance();
        if (agentContext != null && agentContext->AgentInterface.IsAgentActive()) {
            addonId = agentContext->OwnerAddon;
        } else {
            var agentInventoryContext = AgentInventoryContext.Instance();
            if (agentInventoryContext != null && agentInventoryContext->AgentInterface.IsAgentActive()) {
                addonId = agentInventoryContext->OwnerAddonId;
            }
        }

        if (addonId is 0 or > ushort.MaxValue) return Config.Default;
        var addon = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonById((ushort)addonId);
        if (addon == null) return Config.Default;
        var addonName = MemoryHelper.ReadString(new IntPtr(addon->Name), 0x20);
        if (string.IsNullOrEmpty(addonName)) return Config.Default;
        SimpleLog.Log($"Try on from : {addonName}");
        if (Config.WindowSettings.ContainsKey(addonName)) return Config.WindowSettings[addonName];

        if (!newWindows.Contains(addonName)) {
            newWindows.Add(addonName);
        }

        return Config.Default;
    }
    
    private byte TryOnDetour(uint unknownCanEquip, uint itemBaseId, ulong stainColor, uint itemGlamourId, byte unknownByte) {
        var c = GetActiveWindowSettings();
        var i = c.NoModifier;
        if (Service.KeyState[VirtualKey.SHIFT]) i = c.HoldingShift;
        if (i == TryOnItem.Original) return tryOnHook.Original(unknownCanEquip, itemBaseId, stainColor, 0, unknownByte);
        return tryOnHook.Original(unknownCanEquip, itemGlamourId != 0 ? itemGlamourId : itemBaseId, stainColor, 0, unknownByte);
    }

    public override void Disable() {
        SaveConfig(Config);
        tryOnHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        tryOnHook?.Dispose();
        base.Dispose();
    }
}