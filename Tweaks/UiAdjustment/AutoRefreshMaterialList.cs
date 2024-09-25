using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Auto Refresh Material List")]
[TweakDescription("Automatically refreshes the raw material list and recipe tree windows.")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class AutoRefreshMaterialList : Tweak {
    private const int Throttle = 100;

    private readonly Stopwatch recipeMaterialListThrottle = Stopwatch.StartNew();
    private readonly Stopwatch recipeTreeThrottle = Stopwatch.StartNew();

    [AddonFinalize("RecipeMaterialList")]
    [AddonPostSetup("RecipeMaterialList")]
    private void ResetMaterialList() => recipeMaterialListThrottle.Restart();

    [AddonFinalize("RecipeTree")]
    [AddonPostSetup("RecipeTree")]
    private void ResetTree() => recipeTreeThrottle.Restart();

    [AddonPreDraw("RecipeMaterialList")] private void PreDrawRecipeMaterialList() => PreDraw(AgentId.RecipeMaterialList, recipeMaterialListThrottle);
    [AddonPreDraw("RecipeTree")] private void PreDrawRecipeTree() => PreDraw(AgentId.RecipeTree, recipeTreeThrottle);

    private void PreDraw(AgentId agentId, Stopwatch throttle) {
        if (throttle.ElapsedMilliseconds < Throttle) return;
        throttle.Restart();

        var agent = AgentModule.Instance()->GetAgentByInternalId(agentId);
        if (!agent->IsAgentActive()) return;
        if (!agent->IsAddonReady()) return;

        Common.SendEvent(agent, 0, 0);
    }
}
