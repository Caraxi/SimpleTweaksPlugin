using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets2;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[TweakName("Hide Guildhest Objective Popup")]
[TweakAuthor("MidoriKami")]
[TweakDescription("Hides the objective popup when starting a guildhest.")]
[TweakReleaseVersion("1.9.4.0")]
public unsafe class HideGuildhestObjectivePopup : UiAdjustments.SubTweak {
    [AddonPreSetup("JournalAccept")]
    private void JournalAcceptPreSetup(AtkUnitBase* addon) {
        if (Service.Data.GetExcelSheet<TerritoryType>()!.GetRow(Service.ClientState.TerritoryType) is not { TerritoryIntendedUse: 3 }) return;
        addon->Hide(false, false, 1);
    }
}