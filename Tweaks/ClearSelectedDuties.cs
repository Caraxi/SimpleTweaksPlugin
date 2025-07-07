using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Clear Selected Duties")]
[TweakDescription("When opening Duty Finder, unselects any previously selected Duties.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class ClearSelectedDuties : Tweak {

    [AddonPostSetup("ContentsFinder")]
    private void OnDutyFinderOpened(AddonContentsFinder* addon) {
        var returnValue = stackalloc AtkValue[1];
        var command = stackalloc AtkValue[2];
        command[0].SetInt(12);
        command[1].SetInt(1);
                
        AgentContentsFinder.Instance()->ReceiveEvent(returnValue, command, 2, 0);
    }
}
