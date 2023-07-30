using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Game.Config;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class LargeCooldownCounter : UiAdjustments.SubTweak {
    public override string Name => "Adjust Large Cooldown Counter";
    public override string Description => "Increases the size of cooldown counters on hotbars.";

    private delegate void UpdateHotbarSlotCooldownText(AddonActionBarBase* addon, ulong a2, ulong a3, void* a4, uint a5, int a6);
    private HookWrapper<UpdateHotbarSlotCooldownText> updateHotbarSlotCooldownTextHook;

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        updateHotbarSlotCooldownTextHook ??= Common.Hook<UpdateHotbarSlotCooldownText>("E8 ?? ?? ?? ?? 4D 8B CD 89 7C 24 28", UpdateHotbarSlotCooldownTextDetour);
        updateHotbarSlotCooldownTextHook?.Enable();
        base.Enable();
    }
    
    
    public class Configs : TweakConfig {
        public FontType Font = FontType.TrumpGothic;
        public int FontSizeAdjust;
        public Vector4 CooldownColour = new(1, 1, 1, 1);
        public Vector4 CooldownEdgeColour = new(0.2F, 0.2F, 0.2F, 1);
        public Vector4 InvalidColour = new(0.85f, 0.25f, 0.25f, 1);
        public Vector4 InvalidEdgeColour = new(0.34f, 0, 0, 1);
    }

    public Configs Config { get; private set; }
        

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        if (Service.GameConfig.TryGet(UiConfigOption.HotbarDispRecastTimeDispType, out uint v) && v != 1) {
            var warningText = "Your UI is currently configured to use 'Bottom Left' recast timers.\nThis tweak only functions on 'Centered' recast timers.";
            var textSize = ImGui.CalcTextSize(warningText);
            ImGui.PushStyleColor(ImGuiCol.Border, 0x880000FF);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 3);
            if (ImGui.BeginChild("setting_warning", new Vector2(ImGui.GetContentRegionAvail().X, textSize.Y + ImGui.GetStyle().WindowPadding.Y * 2), true)) {
                if (ImGui.Button("Apply Config##largeCooldownFixButton", textSize with { X = 100 * ImGuiHelpers.GlobalScale })) {
                    Service.GameConfig.Set(UiConfigOption.HotbarDispRecastTimeDispType, 1);
                }
                ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X, ImGui.GetItemRectMin().Y));
                ImGui.Text(warningText);
            }
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            ImGui.EndChild();
        }
        
        ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
        if (ImGui.BeginCombo(LocString("Font") + "###st_uiAdjustment_largeCooldownCounter_fontSelect", $"{Config.Font}")) {
            foreach (var f in (FontType[])Enum.GetValues(typeof(FontType))) {
                if ((byte) f >= 4) continue;
                if (ImGui.Selectable($"{f}##st_uiAdjustment_largeCooldownCount_fontOption", f == Config.Font)) {
                    Config.Font = f;
                    hasChanged = true;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.SliderInt(LocString("Font Size Adjust") + "##st_uiAdjustment_largEcooldownCounter_fontSize", ref Config.FontSizeAdjust, -15, 30);
        hasChanged |= ImGui.ColorEdit4(LocString("Text Colour") + "##largeCooldownCounter", ref Config.CooldownColour);
        hasChanged |= ImGui.ColorEdit4(LocString("Edge Colour") + "##largeCooldownCounter", ref Config.CooldownEdgeColour);
        hasChanged |= ImGui.ColorEdit4(LocString("Out Of Range Colour") + "##largeCooldownCounter", ref Config.InvalidColour);
        hasChanged |= ImGui.ColorEdit4(LocString("Out Of Range Edge Colour") + "##largeCooldownCounter", ref Config.InvalidEdgeColour);
    };
    
    private byte DefaultFontSize => Config.Font switch {
        FontType.Axis => 18,
        FontType.MiedingerMed => 14,
        FontType.Miedinger => 15,
        FontType.TrumpGothic => 24,
        _ => 16,
    };
    
    private byte GetFontSize() {
        var s = (Config.FontSizeAdjust * 2) + DefaultFontSize;
        if (s < 4) s = 4;
        if (s > 255) s = 255;
        return (byte) s;
    }
    
    private void UpdateHotbarSlotCooldownTextDetour(AddonActionBarBase* addon, ulong a2, ulong a3, void* a4, uint a5, int a6) {
        updateHotbarSlotCooldownTextHook.Original(addon, a2, a3, a4, a5, a6);
        
        try {
            // E8 ?? ?? ?? ?? 4D 8B CD 89 7C 24 28
            var v7 = *(int*)(*(ulong*)(a3 + 0x20) + 4UL * (a5 + 2));
            if (v7 == 5) {
                var v8 = *(ulong *)(*(ulong *)(a2 + 136UL) + 248UL);
                var cooldownTextNode = *(AtkTextNode**)(v8 + 256);
                if ((byte)Config.Font >= 4) Config.Font = FontType.TrumpGothic;
                
                cooldownTextNode->SetAlignment(AlignmentType.Center);
                cooldownTextNode->SetFont(Config.Font);
                cooldownTextNode->FontSize = GetFontSize();
                
                if (cooldownTextNode->TextColor.B < 100) {
                    cooldownTextNode->TextColor.R = (byte)(Config.InvalidColour.X * 255f);
                    cooldownTextNode->TextColor.G = (byte)(Config.InvalidColour.Y * 255f);
                    cooldownTextNode->TextColor.B = (byte)(Config.InvalidColour.Z * 255f);
                    cooldownTextNode->TextColor.A = (byte)(Config.InvalidColour.W * 255f);

                    cooldownTextNode->EdgeColor.R = (byte)(Config.InvalidEdgeColour.X * 255f);
                    cooldownTextNode->EdgeColor.G = (byte)(Config.InvalidEdgeColour.Y * 255f);
                    cooldownTextNode->EdgeColor.B = (byte)(Config.InvalidEdgeColour.Z * 255f);
                    cooldownTextNode->EdgeColor.A = (byte)(Config.InvalidEdgeColour.W * 255f);
                } else {
                    cooldownTextNode->TextColor.R = (byte)(Config.CooldownColour.X * 255f);
                    cooldownTextNode->TextColor.G = (byte)(Config.CooldownColour.Y * 255f);
                    cooldownTextNode->TextColor.B = (byte)(Config.CooldownColour.Z * 255f);
                    cooldownTextNode->TextColor.A = (byte)(Config.CooldownColour.W * 255f);

                    cooldownTextNode->EdgeColor.R = (byte)(Config.CooldownEdgeColour.X * 255f);
                    cooldownTextNode->EdgeColor.G = (byte)(Config.CooldownEdgeColour.Y * 255f);
                    cooldownTextNode->EdgeColor.B = (byte)(Config.CooldownEdgeColour.Z * 255f);
                    cooldownTextNode->EdgeColor.A = (byte)(Config.CooldownEdgeColour.W * 255f);
                } 
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    protected override void Disable() {
        updateHotbarSlotCooldownTextHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        updateHotbarSlotCooldownTextHook?.Dispose();
        base.Dispose();
    }
}
