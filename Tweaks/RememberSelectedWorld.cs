using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Remember Selected World")]
[TweakDescription("Remembers which world was selected for each datacentre.")]
[TweakReleaseVersion("1.9.1.0")]
[Changelog("1.9.1.1", "Now uses a more reliable method for selecting worlds.")]
public unsafe class RememberSelectedWorld : Tweak {
    private class Configs : TweakConfig {
        public Dictionary<uint, uint> DataCenterWorldSelect = new();
    }

    private Configs? Config { get; set; }
    private uint setting = uint.MaxValue;
    private bool isSetting;

    [AddonPreSetup("CharaSelect")]
    private void AddonSetup() {
        isSetting = true;
        setting = uint.MaxValue;
    }

    [AddonPostUpdate("CharaSelect")]
    public void AddonUpdate(AtkUnitBase* unitBase) {
        var lobby = AgentLobby.Instance();
        if (lobby == null || lobby->WorldId == 0 || lobby->WorldIndex < 0) return;
        Config ??= LoadConfig<Configs>() ?? new Configs();

        if (isSetting) {
            if (setting == uint.MaxValue) {
                if (Config.DataCenterWorldSelect.TryGetValue(lobby->DataCenter, out setting)) return;
                setting = uint.MaxValue;
                isSetting = false;
                return;
            }

            if (setting == lobby->WorldId) {
                isSetting = false;
                return;
            }

            SelectWorld(setting);
            return;
        }

        if (Config.DataCenterWorldSelect.TryGetValue(lobby->DataCenter, out var worldId) && worldId == lobby->WorldId) return;
        Config.DataCenterWorldSelect[lobby->DataCenter] = lobby->WorldId;
        SaveConfig(Config);
    }

    private void SelectWorld(uint worldId) {
        if (!Common.GetUnitBase("_CharaSelectWorldServer", out var addon)) return;

        var stringArray = AtkStage.Instance()->GetStringArrayData()[1];
        if (stringArray == null) return;

        var world = Service.Data.Excel.GetSheet<World>().GetRowOrNull(worldId);
        if (world is not { IsPublic: true }) return;

        SimpleLog.Debug($"Attempting to Select World: {world.Value.Name.ExtractText()}");

        var checkedWorldCount = 0;

        for (var i = 0; i < 16; i++) {
            var n = stringArray->StringArray[i];
            if (n == null) continue;
            var s = Common.ReadString(n);
            if (s.Trim().Length == 0) continue;
            checkedWorldCount++;
            if (s != world.Value.Name.ExtractText()) {
                SimpleLog.Verbose($"'{s}' != '{world.Value.Name.ExtractText()}'");
                continue;
            }

            Common.GenerateCallback(addon, 25, 0, i);
            Common.GenerateCallback(addon, 21, 0, 0);
            return;
        }

        if (checkedWorldCount > 0) {
            SimpleLog.Warning($"World '{world.Value.Name.ExtractText()}' not found.");
            isSetting = false;
        }
    }
}
