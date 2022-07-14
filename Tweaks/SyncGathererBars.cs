using System.Collections.Generic;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class SyncGathererBars : Tweak {
    public override string Name => "Sync Gatherer Bars";
    public override string Description => "Keeps miner and botanist hotbars in sync.";

    private readonly Dictionary<uint, uint>[] actionSwaps = {new() {
        { 227, 210 }, { 210, 227 }, { 228, 211 }, { 211, 228 },
        { 235, 218 }, { 291, 290 }, { 290, 291 }, { 237, 220 },
        { 303, 304 }, { 295, 294 }, { 21177, 21178 }, { 280, 282 },
        { 4072, 4086 }, { 4073, 4087 }, { 232, 215 }, { 239, 222 },
        { 241, 224 }, { 238, 221 }, { 221, 238 }, { 240, 815 },
        { 22182, 22186 }, { 22183, 22187 }, { 22184, 22188 }, { 22185, 22189 },
        { 25589, 25590 }, { 4081, 4095 }, { 272, 273 }, { 4589, 4590 },
        { 21203, 21204 }, { 21205, 21206 }, { 26521, 26522 },
    }, new() {
        { 210, 227 }, { 227, 210 }, { 211, 228 }, { 228, 211 },
        { 218, 235 }, { 290, 291 }, { 291, 290 }, { 220, 237 },
        { 304, 303 }, { 294, 295 }, { 21178, 21177 }, { 282, 280 },
        { 4086, 4072 }, { 4087, 4073 }, { 215, 232 }, { 222, 239 },
        { 224, 241 }, { 221, 238 }, { 238, 221 }, { 815, 240 },
        { 22186, 22182 }, { 22187, 22183 }, { 22188, 22184 }, { 22189, 22185 },
        { 25590, 25589 }, { 4095, 4081 }, { 273, 272 }, { 4590, 4589 },
        { 21204, 21203 }, { 21206, 21205 }, { 26522, 26521 },
    }};

    public class Configs : TweakConfig {
        public bool[] StandardBars = new bool[10];
        public bool[] CrossBars = new bool[8];
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
        if (Config.StandardBars.Length != 10) Config.StandardBars = new bool[10];
        if (Config.CrossBars.Length != 8) Config.CrossBars = new bool[8];

        ImGui.Text("Select bars to sync between miner and botanist.");
        ImGui.Indent();

        var columns = (int) (ImGuiExt.GetWindowContentRegionSize().X / (150f * ImGui.GetIO().FontGlobalScale));

        ImGui.Columns(columns, "hotbarColumns", false);
        for (var i = 0; i < Config.StandardBars.Length; i++) {
            ImGui.Checkbox($"Hotbar {i+1}##syncBar_{i}", ref Config.StandardBars[i]);
            ImGui.NextColumn();
        }
        ImGui.Columns(1);
        ImGui.Columns(columns, "crosshotbarColumns", false);
        for (var i = 0; i < Config.CrossBars.Length; i++) {
            ImGui.Checkbox($"Cross Hotbar {i+1}##syncCrossBar_{i}", ref Config.CrossBars[i]);
            ImGui.NextColumn();
        }
        ImGui.Columns(1);
        ImGui.Unindent();

    };

    private int checkBar = -1;
    private bool onGatherer;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Framework.Update += FrameworkOnUpdate;
        base.Enable();
    }

    private void FrameworkOnUpdate(Framework framework) {
        if (++checkBar >= 18) checkBar = 0;
        if (checkBar < 10 && !Config.StandardBars[checkBar]) return;
        if (checkBar >= 10 && !Config.CrossBars[checkBar - 10]) return;

        if (onGatherer == false && checkBar == 0) {
            if (Service.ClientState.LocalPlayer?.ClassJob.Id is 16 or 17) {
                onGatherer = true;
            }
        }
        if (!onGatherer) return;

        if (Service.ClientState.LocalPlayer?.ClassJob.Id is not (16 or 17)) {
            onGatherer = false;
            return;
        }

        var hotbarModule = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();

        var currentId = (int)Service.ClientState.LocalPlayer?.ClassJob.Id;
        if (currentId is not (16 or 17)) return;
        var otherId = currentId is 16 ? 17 : 16;

        var swapDict = actionSwaps[currentId is 16 ? 0 : 1];
        var current = hotbarModule->SavedClassJob[currentId];
        var other = hotbarModule->SavedClassJob[otherId];

        var cBar = current->Bar[checkBar];
        var oBar = other->Bar[checkBar];

        for (var i = 0; i < 16; i++) {
            var cSlot = cBar->Slot[i];
            var oSlot = oBar->Slot[i];
            oSlot->Type = cSlot->Type;
            if (cSlot->Type == HotbarSlotType.Action) {
                if (swapDict.ContainsKey(cSlot->ID)) {
                    oSlot->ID = swapDict[cSlot->ID];
                } else {
                    oSlot->ID = cSlot->ID;
                }
            } else {
                oSlot->ID = cSlot->ID;
            }
        }
    }

    public override void Disable() {
        Service.Framework.Update -= FrameworkOnUpdate;
        SaveConfig(Config);
        base.Disable();
    }

}