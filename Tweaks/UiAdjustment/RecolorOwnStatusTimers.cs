using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class RecolorOwnStatusTimers : UiAdjustments.SubTweak {
    public override string Name => "Recolor Own Status Timers";
    public override string Description => "Allows the recoloring of the personal status timers color.";
    protected override string Author => "Aireil";

    private delegate byte GetUiColor(uint colorIndex, ByteColor* color, ByteColor* edgeColor);
    private HookWrapper<GetUiColor> getUiColorHook;

    public class Configs : TweakConfig {
        public Vector4 OwnStatusColor = new Vector4(201, 255, 228, 255) / 255f;
        public Vector4 OwnStatusEdgeColor = new Vector4(10, 95, 36, 255) / 255f;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.ColorEdit4("Color##colorEditStatusTimers", ref Config.OwnStatusColor);
        hasChanged |= ImGui.ColorEdit4("Edge Color##edgeColorEditStatusTimers", ref Config.OwnStatusEdgeColor);

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Reset: ");

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##resetStatusTimerColors")) {
            Config.OwnStatusColor = new Vector4(201, 255, 228, 255) / 255f;
            Config.OwnStatusEdgeColor = new Vector4(10, 95, 36, 255) / 255f;
            hasChanged = true;
        }
        ImGui.PopFont();
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        getUiColorHook ??= Common.Hook<GetUiColor>("83 F9 0A 73 26", GetUiColorDetour);
        getUiColorHook?.Enable();
        base.Enable();
    }

    private byte GetUiColorDetour(uint colorIndex, ByteColor* color, ByteColor* edgeColor) {
        try {
            if (colorIndex == 2) {
                var ret = getUiColorHook.Original(colorIndex, color, edgeColor);

                (*color).R = (byte)(Config.OwnStatusColor.X * 255f);
                (*color).G = (byte)(Config.OwnStatusColor.Y * 255f);
                (*color).B = (byte)(Config.OwnStatusColor.Z * 255f);
                (*color).A = (byte)(Config.OwnStatusColor.W * 255f);
                (*edgeColor).R = (byte)(Config.OwnStatusEdgeColor.X * 255f);
                (*edgeColor).G = (byte)(Config.OwnStatusEdgeColor.Y * 255f);
                (*edgeColor).B = (byte)(Config.OwnStatusEdgeColor.Z * 255f);
                (*edgeColor).A = (byte)(Config.OwnStatusEdgeColor.W * 255f);

                return ret;
            }
        } catch {
            //
        }

        return getUiColorHook.Original(colorIndex, color, edgeColor);
    }

    public override void Disable() {
        getUiColorHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        getUiColorHook?.Dispose();
        base.Dispose();
    }
}
