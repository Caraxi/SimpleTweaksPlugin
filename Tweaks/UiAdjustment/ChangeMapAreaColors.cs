using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Components;
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

    public class MapColorConfig : TweakConfig {
        public class AreaEntry {
            public bool Enabled = false;
            public Vector4 OverrideColor = new(0, 0, 0, 0);
        }

        public bool RaveMode = false;

        public readonly Dictionary<int, AreaEntry> Areas = new();
    }

    public enum AreaType {
        None = 0,
        Quest = 1,
        Levequest = 2,
        FATE = 3
        
        // 4  - Pale blue
        // 5  - Red
        // 6  - Yellow
        // 7  - Yellow
        // 8  - Cyan
        // 9  - Red
        // 10 - Light Pink
    }

    public MapColorConfig? Config { get; private set; }

    private delegate void SetMapCircleColor(AreaType areaType, AtkResNode* atkResNode);

    private HookWrapper<SetMapCircleColor>? _setCircleMapColorHook;

    private byte _raveHue;

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
        ImGui.TextWrapped("Note: Map colors will only update upon closing and re-opening the map. The minimap will update instantly.");

        if (ImGui.BeginTable("configTable", 3, ImGuiTableFlags.Borders)) {
            ImGui.TableSetupColumn("Area Type");
            ImGui.TableSetupColumn("Enabled");
            ImGui.TableSetupColumn("Color");
            ImGui.TableHeadersRow();

            foreach (var enumVal in Enum.GetValues<AreaType>()) {
                if (enumVal == AreaType.None) continue;
                ImGui.TableNextRow();
                ImGui.PushID($"entry-{enumVal}");
                var edited = false;
                
                MapColorConfig.AreaEntry? thisConfigEntry = default;
                this.Config?.Areas.TryGetValue((int) enumVal, out thisConfigEntry) ;
                var enabled = thisConfigEntry?.Enabled ?? false;
                var overrideColor = thisConfigEntry?.OverrideColor ?? default;

                ImGui.TableSetColumnIndex(0);
                ImGui.Text(enumVal.ToString());

                ImGui.TableSetColumnIndex(1);
                edited |= ImGui.Checkbox("##enabled", ref enabled);

                ImGui.TableSetColumnIndex(2);
                var newColor = ImGuiComponents.ColorPickerWithPalette((int) enumVal, "##color", overrideColor, ImGuiColorEditFlags.NoAlpha);
                edited |= !overrideColor.Equals(newColor);

                if (edited) {
                    this.Config!.Areas[(int) enumVal] = new MapColorConfig.AreaEntry {
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
        if (ImGui.Checkbox("RAVE MODE (Minimap only, sorry!)", ref raveMode)) {
            this.Config!.RaveMode = raveMode;
            this._raveHue = 0;

            hasChanged = true;
        }
    };

    private void SetMapCircleColorDetour(AreaType areaType, AtkResNode* node) {
        if (areaType == AreaType.None || node == null || this.Config == null) {
            this._setCircleMapColorHook?.Original(areaType, node);
            return;
        }

        if (this.Config.RaveMode) {
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
        node->MultiplyRed = node->MultiplyGreen = node->MultiplyBlue = 0;

        node->AddRed = (byte) (color.X * 255);
        node->AddGreen = (byte) (color.Y * 255);
        node->AddBlue = (byte) (color.Z * 255);
    }
}