using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Remember Selected World")]
[TweakDescription("Remembers which world was selected for each datacentre.")]
[TweakReleaseVersion("1.9.1.0")]
public unsafe class RememberSelectedWorld : Tweak {
    private class Configs : TweakConfig {
        public Dictionary<uint, short> DataCenterWorldSelect = new();
    }

    private Configs? Config { get; set; }
    private short setting = -1;
    private bool isSetting;
    
    [AddonPreSetup("CharaSelect")]
    private void AddonSetup() {
        isSetting = true;
        setting = -1;
    }

    [AddonPostUpdate("CharaSelect")]
    public void AddonUpdate(AtkUnitBase* unitBase) {
        var lobby = AgentLobby.Instance();
        if (lobby == null || lobby->WorldId == 0 || lobby->WorldIndex < 0) return;
        Config ??= LoadConfig<Configs>() ?? new Configs();

        if (isSetting) {
            if (setting == -1) {
                if (Config.DataCenterWorldSelect.TryGetValue(lobby->DataCenter, out setting)) return;
                isSetting = false;
                return;
            }

            if (setting == lobby->WorldIndex) {
                isSetting = false;
                return;
            }
            SimpleLog.Verbose($"Setting selected world to Index#{setting}");
            Common.GenerateCallback(unitBase, 10, 0, (int) setting);
            Common.GenerateCallback(unitBase, 6, 0, 0);
            setting = -1;
            return;
        }

        if (Config.DataCenterWorldSelect.TryGetValue(lobby->DataCenter, out var worldIndex) && worldIndex == lobby->WorldIndex) return;
        Config.DataCenterWorldSelect[lobby->DataCenter] = lobby->WorldIndex;
        SaveConfig(Config);
    }
}
