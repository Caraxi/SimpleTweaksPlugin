using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[TweakName("Battle Talk Adjustments")]
[TweakDescription("Allows moving of the dialogue box that appears in the middle of battles.")]
[TweakAuthor("Chivalrik")]
[TweakAutoConfig]
public unsafe class BattleTalkAdjustments : UiAdjustments.SubTweak {
    
    public override IEnumerable<string> Tags => new[] { "BattleTalk" };

    public class Configuration : TweakConfig {
        public static readonly Configuration Default = new();
        
        public Vector2 TextPosition = new(0, 0);
        public float Scale = 1;
    }

    public Configuration Config { get; private set; }

    private float originalPositionX = 0f;
    private float originalPositionY = 0f;

    protected void DrawConfig() {
        ImGui.DragFloat2("Text Position", ref Config.TextPosition);
    }
    
    [FrameworkUpdate]
    private void OnFrameworkUpdate() => Update(Config);

    private void Update(Configuration config) {
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse
                                       | ImGuiWindowFlags.NoDecoration
                                       | ImGuiWindowFlags.NoNav
                                       | ImGuiWindowFlags.NoFocusOnAppearing
                                       | ImGuiWindowFlags.NoTitleBar
                                       | ImGuiWindowFlags.NoInputs
                                       | ImGuiWindowFlags.NoMouseInputs
                                       | ImGuiWindowFlags.NoCollapse
                                       | ImGuiWindowFlags.NoSavedSettings;
        var battleTalkResNode = GetBattleTalkNode();
        if (battleTalkResNode == null) return;
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(battleTalkResNode->X, battleTalkResNode->Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(
            new Vector2(battleTalkResNode->Width * battleTalkResNode->ScaleX, battleTalkResNode->Height* battleTalkResNode->ScaleX)
            , ImGuiCond.Always);
        ImGui.Begin("###BattleTalkAdjustments_PreviewWindow", flags);
        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
        ImGui.Dummy(new Vector2(25,0) * ImGui.GetIO().FontGlobalScale);
        ImGui.Text(LocString("PositionPreviewText", "NEW BATTLE TALK DIALOGUE BOX POSITION"));
        ImGui.End();
        
        UiHelper.SetPosition(battleTalkResNode, originalPositionX + config.TextPosition.X, originalPositionY + config.TextPosition.Y);
        battleTalkResNode->SetScale(config.Scale, config.Scale);
    }

    protected override void Disable() => Update(Configuration.Default);
        
    private static AtkResNode* GetBattleTalkNode() {
        var battleTalkUnitBase = Common.GetUnitBase("_BattleTalk");
        if (battleTalkUnitBase == null) return null;
        return battleTalkUnitBase->UldManager.NodeList == null ? null : battleTalkUnitBase->UldManager.NodeList[0];
    }
        
        
}