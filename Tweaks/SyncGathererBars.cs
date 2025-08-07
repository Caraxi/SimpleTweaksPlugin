using System.Collections.Generic;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Sync Gatherer Bars")]
[TweakDescription("Keeps miner and botanist hotbars in sync.")]
[TweakCategory(TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class SyncGathererBars : Tweak {
    private readonly Dictionary<uint, uint>[] actionSwaps = [
        new() {
            { 227, 210 },
            { 210, 227 },
            { 228, 211 },
            { 211, 228 },
            { 235, 218 },
            { 291, 290 },
            { 290, 291 },
            { 237, 220 },
            { 303, 304 },
            { 295, 294 },
            { 21177, 21178 },
            { 280, 282 },
            { 4072, 4086 },
            { 4073, 4087 },
            { 232, 215 },
            { 239, 222 },
            { 241, 224 },
            { 238, 221 },
            { 221, 238 },
            { 240, 815 },
            { 22182, 22186 },
            { 22183, 22187 },
            { 22184, 22188 },
            { 22185, 22189 },
            { 25589, 25590 },
            { 4081, 4095 },
            { 272, 273 },
            { 4589, 4590 },
            { 21203, 21204 },
            { 21205, 21206 },
            { 26521, 26522 },
            { 34871, 34872 },
        },
        new Dictionary<uint, uint> {
            { 210, 227 },
            { 227, 210 },
            { 211, 228 },
            { 228, 211 },
            { 218, 235 },
            { 290, 291 },
            { 291, 290 },
            { 220, 237 },
            { 304, 303 },
            { 294, 295 },
            { 21178, 21177 },
            { 282, 280 },
            { 4086, 4072 },
            { 4087, 4073 },
            { 215, 232 },
            { 222, 239 },
            { 224, 241 },
            { 221, 238 },
            { 238, 221 },
            { 815, 240 },
            { 22186, 22182 },
            { 22187, 22183 },
            { 22188, 22184 },
            { 22189, 22185 },
            { 25590, 25589 },
            { 4095, 4081 },
            { 273, 272 },
            { 4590, 4589 },
            { 21204, 21203 },
            { 21206, 21205 },
            { 26522, 26521 },
            { 34872, 34871 },
        }
    ];

    public class Configs : TweakConfig {
        public bool[] StandardBars = new bool[10];
        public bool[] CrossBars = new bool[8];
    }

    [TweakConfig] public Configs Config { get; private set; }

    public bool IsShared(int number, bool cross) {
        var name = $"Hotbar{(cross ? "Cross" : "")}Common{number + 1:00}";
        if (Service.GameConfig.UiConfig.TryGetBool(name, out var v)) {
            return v;
        }

        return false;
    }

    protected void DrawConfig(ref bool _) {
        if (Config.StandardBars.Length != 10) Config.StandardBars = new bool[10];
        if (Config.CrossBars.Length != 8) Config.CrossBars = new bool[8];

        ImGui.Text("Select bars to sync between miner and botanist.");
        ImGui.Indent();

        var columns = (int)(ImGuiExt.GetWindowContentRegionSize().X / (150f * ImGui.GetIO().FontGlobalScale));

        ImGui.Columns(columns, "hotbarColumns", false);
        for (var i = 0; i < Config.StandardBars.Length; i++) {
            var isShared = IsShared(i, false);
            using (ImRaii.Disabled(isShared)) {
                ImGui.Checkbox($"Hotbar {i + 1}##syncBar_{i}", ref Config.StandardBars[i]);
            }

            if (isShared && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                ImGui.SetTooltip("Shared Hotbars will not be synced");
            }

            if (isShared && Config.StandardBars[i]) {
                using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudYellow)) {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker("Shared Hotbars will not be synced");
                }
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.Columns(columns, "crosshotbarColumns", false);
        for (var i = 0; i < Config.CrossBars.Length; i++) {
            var isShared = IsShared(i, true);
            using (ImRaii.Disabled(isShared)) {
                ImGui.Checkbox($"Cross Hotbar {i + 1}##syncCrossBar_{i}", ref Config.CrossBars[i]);
            }

            if (isShared && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                ImGui.SetTooltip("Shared Cross Hotbars will not be synced");
            }

            if (isShared && Config.CrossBars[i]) {
                using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudYellow)) {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker("Shared Cross Hotbars will not be synced");
                }
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.Unindent();
    }

    private int checkBar = -1;
    private bool onGatherer;

    [FrameworkUpdate]
    private void FrameworkOnUpdate() {
        if (++checkBar >= 18) checkBar = 0;
        if (checkBar < 10 && !Config.StandardBars[checkBar]) return;
        if (checkBar >= 10 && !Config.CrossBars[checkBar - 10]) return;

        if (onGatherer == false && checkBar == 0) {
            if (Service.ClientState.LocalPlayer?.ClassJob.RowId is 16 or 17) {
                onGatherer = true;
            }
        }

        if (!onGatherer) return;

        if (Service.ClientState.LocalPlayer?.ClassJob.RowId is not (16 or 17)) {
            onGatherer = false;
            return;
        }

        var hotbarModule = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->UIModule->GetRaptureHotbarModule();
        if (Service.ClientState.LocalPlayer == null) return;
        var currentId = (int)Service.ClientState.LocalPlayer.ClassJob.RowId;
        if (currentId is not (16 or 17)) return;

        if (IsShared(checkBar < 10 ? checkBar : checkBar - 10, checkBar >= 10)) return;

        var otherId = currentId is 16 ? 17 : 16;

        var swapDict = actionSwaps[currentId is 16 ? 0 : 1];
        var current = hotbarModule->SavedHotbars.GetPointer(currentId);
        var other = hotbarModule->SavedHotbars.GetPointer(otherId);

        var cBar = current->Hotbars.GetPointer(checkBar);
        var oBar = other->Hotbars.GetPointer(checkBar);

        for (var i = 0; i < 16; i++) {
            var cSlot = cBar->Slots.GetPointer(i);
            var oSlot = oBar->Slots.GetPointer(i);
            oSlot->CommandType = cSlot->CommandType;
            if (cSlot->CommandType == RaptureHotbarModule.HotbarSlotType.Action) {
                if (swapDict.ContainsKey(cSlot->CommandId)) {
                    oSlot->CommandId = swapDict[cSlot->CommandId];
                } else {
                    oSlot->CommandId = cSlot->CommandId;
                }
            } else {
                oSlot->CommandId = cSlot->CommandId;
            }
        }
    }
}
