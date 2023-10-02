using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class HideQualityWhenNoHQ : UiAdjustments.SubTweak {
    public override string Name => "Hide quality bar while crafting NO-HQ item.";
    public override string Description => "Hides the quality bar in the Synthesis window while crafting an item that can not be HQ or Collectable.";

    public override void Setup() {
        AddChangelogNewTweak("1.8.4.0");
        AddChangelog("1.8.9.0", "Show quality bar for expert recipes.");
        base.Setup();
    }
    
    [AddonPostSetup("Synthesis")]
    private void CommonOnAddonSetup(AtkUnitBase* addon) {
        var agent = AgentRecipeNote.Instance();
        var recipe = Service.Data.GetExcelSheet<Recipe>()?.GetRow(agent->ActiveCraftRecipeId);
        if (recipe == null) return;
        if (recipe.CanHq || recipe.IsExpert) return;
        var qualityNode = addon->GetNodeById(58);
        if (qualityNode == null) return;
        qualityNode->ToggleVisibility(false);
    }

    protected override void Disable() {
        // Immediately reshow
        var addon = Common.GetUnitBase("Synthesis");
        if (addon != null) {
            var qualityNode = addon->GetNodeById(58);
            if (qualityNode != null) qualityNode->ToggleVisibility(true);
        }

        base.Disable();
    }
}
