using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Battle Talk Adjustments")]
[TweakDescription("Allows moving of the dialogue box that appears in the middle of battles.")]
[TweakAuthor("Chivalrik")]
[TweakTags("BattleTalk")]
[TweakAutoConfig]
public unsafe class BattleTalkAdjustments : UiAdjustments.SubTweak {
    public class Configuration : TweakConfig {
        public int OffsetX;
        public int OffsetY;
        public float Scale = 1;
    }

    public Configuration Config { get; private set; }
    private readonly Stopwatch timeSincePreview = new();

    protected void DrawConfig(ref bool changed) {
        ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.DragInt(LocString("X Offset") + "##battletalkadjustments_offsetPosition", ref Config.OffsetX, 1);
        ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.DragInt(LocString("Y Offset") + "##battletalkadjustments_offsetPosition", ref Config.OffsetY, 1);
        ImGui.SetNextItemWidth(200 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.SliderFloat("##battletalkadjustments_Scale", ref Config.Scale, 0.01f, 3f, LocString("Scale") + ": %.2fx");

        if (!changed || (timeSincePreview.ElapsedMilliseconds <= 10000 && timeSincePreview.IsRunning)) return;
        if (!Common.GetUnitBase("_BattleTalk", out var bt) || bt->IsVisible) return;
        
        UIModule.Instance()->ShowBattleTalk("Simple Tweaks", "This is a preview of the Battle Talk adjustments.", 10f, 1);
        timeSincePreview.Restart();
    }

    [AddonPreDraw("_BattleTalk")]
    private void PreDraw(AtkUnitBase* unitBase) {
        if (unitBase == null) return;
        if (!unitBase->IsVisible) return;
        var battleTalkResNode = unitBase->RootNode;
        if (battleTalkResNode == null) return;
        battleTalkResNode->SetPositionFloat(unitBase->X + Config.OffsetX, unitBase->Y + Config.OffsetY);
        battleTalkResNode->SetScale(Config.Scale, Config.Scale);
        EventController.EnableEvent(this, AddonEvent.PostUpdate, "_BattleTalk", nameof(PostDraw));
    }

    [AddonPostUpdate("_BattleTalk")]
    private void PostDraw(AtkUnitBase* unitBase) {
        if (unitBase == null) return;
        var battleTalkResNode = unitBase->RootNode;
        if (battleTalkResNode == null) return;
        battleTalkResNode->SetPositionShort(unitBase->X, unitBase->Y);
        battleTalkResNode->SetScale(1, 1);

        if (!unitBase->IsVisible) EventController.DisableEvent(this, AddonEvent.PostUpdate, "_BattleTalk", nameof(PostDraw));
    }

    protected override void Disable() => PostDraw(Common.GetUnitBase("_BattleTalk"));
}
