using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Hide quality bar while crafting NO-HQ item.")]
[TweakDescription("Hides the quality bar in the Synthesis window while crafting an item that can not be HQ or Collectable.")]
[TweakReleaseVersion("1.8.4.0")]
[Changelog("1.8.9.0", "Show quality bar for expert recipes.")]
public unsafe class HideQualityWhenNoHQ : UiAdjustments.SubTweak {
    [AddonPostSetup("Synthesis")]
    private void CommonOnAddonSetup(AtkUnitBase* addon) {
        var agent = AgentRecipeNote.Instance();
        var recipe = Service.Data.GetExcelSheet<Recipe>().GetRowOrDefault(agent->ActiveCraftRecipeId);
        if (recipe == null) return;
        if (recipe.Value.CanHq || recipe.Value.IsExpert) return;
        var qualityNode = addon->GetNodeById(58);
        if (qualityNode == null) return;
        qualityNode->ToggleVisibility(false);
    }

    protected override void Disable() {
        if (Common.GetUnitBase("Synthesis", out var addon)) {
            var qualityNode = addon->GetNodeById(58);
            if (qualityNode != null) qualityNode->ToggleVisibility(true);
        }
    }
}
