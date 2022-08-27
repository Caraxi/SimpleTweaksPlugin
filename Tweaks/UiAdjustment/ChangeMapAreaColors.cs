﻿using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Immutable;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ChangeMapAreaColors : UiAdjustments.SubTweak {
    public override string Name => "Change Map Area Colors";

    public override string Description =>
        "Allows players to change the color of map areas like quest targets and FATEs.";

    protected override string Author => "KazWolfe";

    private const string SetMapCircleColorSignature = "E8 ?? ?? ?? ?? 48 8B 0F 33 C0";

    private class MapColorConfig : TweakConfig {
        public class AreaEntry {
            public bool Enabled = false;
            public Vector4 OverrideColor = new(0, 0, 0, 0);
        }

        public bool RaveMode = false;

        public readonly Dictionary<int, AreaEntry> Areas = new();
    }

    [AttributeUsage(AttributeTargets.Field)]
    private class AreaTypeAttribute : Attribute {
        public string Name { get; }
        public string? Description { get; }

        public AreaTypeAttribute(string name, string? description = null) {
            this.Name = name;
            this.Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    private class AreaTypeDefaultColorAttribute : Attribute {
        public Vector4? DefaultColor { get; }

        public AreaTypeDefaultColorAttribute(byte r, byte g, byte b) {
            this.DefaultColor = new Vector4(r / 255f, g / 255f, b / 255f, 1);
        }
    }

    private record AreaTypeMeta {
        public string? Name;
        public string? Description;
        public Vector4 DefaultColor;
    }

    private enum AreaType {
        None = 0,
        
        [AreaType("Quest Area Markers", "Marks the approximate location of certain quest objectives.")]
        [AreaTypeDefaultColor(0xFF, 0x86, 0x00)] // outer color verified in game
        Quest = 1,
        
        [AreaType("Levequest Area Markers", "Marks the approximate location of certain levequest objectives.")]
        [AreaTypeDefaultColor(0x00, 0x6C, 0x00)] // outer color verified in game
        Levequest = 2,
        
        // ReSharper disable once InconsistentNaming
        [AreaType("Fate Boundaries", "Marks the boundary for a FATE.")]
        [AreaTypeDefaultColor(0x00, 0xC9, 0xFF)] // outer color verified in game
        FATE = 3,
        
        [AreaType("Gathering Nodes", "Marks approximate locations of mining, farming, and fishing nodes.")]
        [AreaTypeDefaultColor(0xA5, 0xFF, 0xFF)] // calculated, outer color is #FFFFFF
        GatheringNode = 4,
        
        [AreaType("PvP Red, Bozja CEs", "When in Frontline, shows areas under the Maelstrom's control." +
                                        "\nUsed for Critical Encounters in Bozja.")]
        [AreaTypeDefaultColor(0x7D, 0x00, 0x00)] // outer color verified in game
        MaelstromRed = 5,  // Also used for Bozja CEs.
        
        [AreaType("PvP Yellow", "When in Frontline, marks areas under the Twin Adders' control.")]
        [AreaTypeDefaultColor(0xFF, 0xFF, 0x00)] // outer color verified in game
        AdderYellow = 6,
        
        [AreaType("PvP Blue", "When in Frontline, marks areas under the Immortal Flames' control.")]
        [AreaTypeDefaultColor(0x00, 0xC9, 0xFF)] // outer color verified in game
        FlameBlue = 7,
        
        // Rival Wings (maybe?)
        [AreaType("Rival Wings - Falcons", "When in Rival Wings, marks the Falcons' control of objectives.")]
        [AreaTypeDefaultColor(0x00, 0xC9, 0xFF)] // outer color verified in game
        PvPFalcons = 8,
        
        [AreaType("Rival Wings - Ravens", "When in Rival Wings, marks the Ravens' control of objectives.")]
        [AreaTypeDefaultColor(0x7D, 0x00, 0x00)] // outer color verified in game
        PvPRavens = 9,
        
        [AreaType("Quest Effect Boundaries", "Marks the boundary for specific quests and their status effects.")]
        [AreaTypeDefaultColor(0xFF, 0xC5, 0xFF)] // calculated, outer color is #FFFFFF
        QuestBoundary = 10,
    }

    private readonly ImmutableDictionary<AreaType, AreaTypeMeta> _typeNames;

    private MapColorConfig? Config { get; set; }

    private delegate void SetMapCircleColor(AreaType areaType, AtkResNode* atkResNode);

    private HookWrapper<SetMapCircleColor>? _setCircleMapColorHook;

    private byte _raveHue;

    public ChangeMapAreaColors() {
        this._typeNames = GetConfigurableAreaTypes();
    }

    public override void Enable() {
        this.Config = this.LoadConfig<MapColorConfig>() ?? new MapColorConfig();

        this._setCircleMapColorHook ??=
            Common.Hook<SetMapCircleColor>(SetMapCircleColorSignature, this.SetMapCircleColorDetour);
        this._setCircleMapColorHook.Enable();

        base.Enable();
    }

    public override void Disable() {
        this._setCircleMapColorHook?.Disable();

        this.SaveConfig(this.Config);
        base.Disable();
    }

    public override void Dispose() {
        this._setCircleMapColorHook?.Dispose();
        
        base.Dispose();
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.TextWrapped("Note: Map colors will only update upon closing and re-opening the map. The minimap " +
                          "will update instantly. Actual displayed colors may differ slightly due to transparency " +
                          "effects.");

        if (ImGui.BeginTable("configTable", 3, 
                ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX)) {
            ImGui.TableSetupColumn("On?");
            ImGui.TableSetupColumn("Area Type");
            ImGui.TableSetupColumn("New Color");
            ImGui.TableHeadersRow();

            foreach (var (areaType, areaTypeMeta) in this._typeNames) {
                if (areaType == AreaType.None) continue;

                ImGui.TableNextRow();
                ImGui.PushID($"entry-{areaType}");
                var edited = false;
                
                MapColorConfig.AreaEntry? thisConfigEntry = default;
                this.Config?.Areas.TryGetValue((int) areaType, out thisConfigEntry) ;
                var enabled = thisConfigEntry?.Enabled ?? false;
                var overrideColor = thisConfigEntry?.OverrideColor ?? areaTypeMeta.DefaultColor;
                
                ImGui.TableSetColumnIndex(0);
                edited |= ImGui.Checkbox("##enabled", ref enabled);

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(areaTypeMeta.Name);
                if (areaTypeMeta.Description != null) {
                    ImGuiComponents.HelpMarker(areaTypeMeta.Description);
                }

                ImGui.TableSetColumnIndex(2);
                var newColor = ImGuiComponents.ColorPickerWithPalette((int) areaType, "##color", overrideColor, ImGuiColorEditFlags.NoAlpha);
                edited |= !overrideColor.Equals(newColor);

                if (edited) {
                    this.Config!.Areas[(int) areaType] = new MapColorConfig.AreaEntry {
                        Enabled = enabled,
                        OverrideColor = newColor
                    };

                    hasChanged = true;
                }
                
                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        
        var raveMode = this.Config?.RaveMode ?? false;
        if (ImGui.Checkbox("RAVE MODE", ref raveMode)) {
            this.Config!.RaveMode = raveMode;
            this._raveHue = 0;

            hasChanged = true;
        }
        
        ImGuiComponents.HelpMarker("Rave Mode was a debug mode that was entertaining enough to make it into " +
                                   "the final tweak. It makes map areas on the minimap taste the rainbow, and " +
                                   "will change colors in the map window in a random-ish fashion whenever the map " +
                                   "window is opened or updated. Different types of map areas may share colors if " +
                                   "this option is enabled. \n\n" +
                                   "This setting will have no effect in PVP.");
        
        if (ImGui.Button("Reset Config")) {
            this.Config!.Areas.Clear();
            this.Config.RaveMode = false;
            hasChanged = true;
        }
    };

    private void SetMapCircleColorDetour(AreaType areaType, AtkResNode* node) {
        if (areaType == AreaType.None || node == null || this.Config == null) {
            this._setCircleMapColorHook?.Original(areaType, node);
            return;
        }
        
        // PluginLog.Debug($"Called for AreaType: {(int) areaType}");

        if (this.Config.RaveMode && !Service.ClientState.IsPvP) {
            ImGui.ColorConvertHSVtoRGB(this._raveHue / 255f, 1f, 1f, out var r, out var g, out var b);
            SetNodeColor(node, new Vector4(r, g, b, 1));

            this._raveHue += 1;
            
            return;
        }

        if (!this.Config.Areas.ContainsKey((int) areaType) || !this.Config.Areas[(int) areaType].Enabled) {
            this._setCircleMapColorHook?.Original(areaType, node);
            return;
        }
        
        SetNodeColor(node, this.Config.Areas[(int) areaType].OverrideColor);
    }

    private static void SetNodeColor(AtkResNode* node, Vector4 color) {
        node->MultiplyRed = node->MultiplyGreen = node->MultiplyBlue = 0x00;
         
        node->AddRed = (byte) (color.X * 255);
        node->AddGreen = (byte) (color.Y * 255);
        node->AddBlue = (byte) (color.Z * 255);
    }

    private static ImmutableDictionary<AreaType, AreaTypeMeta> GetConfigurableAreaTypes() {
        var result = new Dictionary<AreaType, AreaTypeMeta>();

        foreach (var areaType in Enum.GetValues<AreaType>()) {
            var areaTypeAttrValue = areaType.GetAttribute<AreaTypeAttribute>();
            if (areaTypeAttrValue == null)  // this *can* be null. the compiler seeks to mislead you
                continue;

            var areaTypeColorAttrValue = areaType.GetAttribute<AreaTypeDefaultColorAttribute>();

            result[areaType] = new AreaTypeMeta {
                Name = areaTypeAttrValue.Name,
                Description = areaTypeAttrValue.Description,
                DefaultColor = areaTypeColorAttrValue?.DefaultColor ?? Vector4.Zero
            };
        }

        return result.ToImmutableDictionary();
    }
}