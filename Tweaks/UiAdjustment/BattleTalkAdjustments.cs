using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Battle Talk Adjustments")]
[TweakDescription("Allows moving of the dialogue box that appears in the middle of battles.")]
[TweakAuthor("Chivalrik")]
public unsafe class BattleTalkAdjustments : UiAdjustments.SubTweak {
    public class Configuration : TweakConfig {
        public int OffsetX = 0;
        public int OffsetY = 0;
        public float Scale = 1;
    }

    public Configuration Config { get; private set; }

    private float originalPositionX = 0f;
    private float originalPositionY = 0f;
        
    protected void DrawConfig(ref bool changed) {
        ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.InputInt(LocString("X Offset") + "##battletalkadjustments_offsetPosition", ref Config.OffsetX, 1);
        ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.InputInt(LocString("Y Offset") + "##battletalkadjustments_offsetPosition", ref Config.OffsetY, 1);
        ImGui.SetNextItemWidth(200 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.SliderFloat("##battletalkadjustments_Scale", ref Config.Scale, 0.01f, 3f, LocString("Scale") + ": %.2fx");
            
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
        if (!changed) return;
        Common.FrameworkUpdate -= FrameworkOnUpdate;
        Common.FrameworkUpdate += FrameworkOnUpdate;
    }

    protected override void Enable() {
        Config = LoadConfig<Configuration>() ?? new Configuration();
        Service.ClientState.Login += OnLogin;
        Service.ClientState.Logout += OnLogout;
        if(Service.ClientState.LocalPlayer is not null) OnLogin();
        base.Enable();
    }

    protected override void Disable() {
        Service.ClientState.Login -= OnLogin;
        Service.ClientState.Logout -= OnLogout;
        OnLogout();
        SaveConfig(Config);
        base.Disable();
    }

    private void OnLogin()
    {
        Common.FrameworkUpdate += FrameworkOnUpdateSetup;
    }
        
    private void OnLogout()
    {
        Common.FrameworkUpdate -= FrameworkOnUpdate;
        Common.FrameworkUpdate -= FrameworkOnUpdateSetup;
        ResetBattleTalk();
    }
        
    private void FrameworkOnUpdateSetup()
    {
        try {
            var battleTalkNode = GetBattleTalkNode();
            if (battleTalkNode == null) return;
            originalPositionX = battleTalkNode->X;
            originalPositionY = battleTalkNode->Y;
            Common.FrameworkUpdate -= FrameworkOnUpdateSetup;
            Common.FrameworkUpdate += FrameworkOnUpdate;
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
    private void FrameworkOnUpdate() {
        try {
            var battleTalkNode = GetBattleTalkNode();
            if (battleTalkNode == null) return;
            UiHelper.SetPosition(battleTalkNode, originalPositionX + Config.OffsetX, originalPositionY + Config.OffsetY);
            battleTalkNode->SetScale(Config.Scale, Config.Scale);
            Common.FrameworkUpdate -= FrameworkOnUpdate;
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
        
    private void ResetBattleTalk()
    {
        var battleTalkNode = GetBattleTalkNode();
        if (battleTalkNode == null) return;
        UiHelper.SetPosition(battleTalkNode, originalPositionX, originalPositionY);
        battleTalkNode->SetScale(1, 1);
    }
        
    private static AtkResNode* GetBattleTalkNode() {
        var battleTalkUnitBase = Common.GetUnitBase("_BattleTalk");
        if (battleTalkUnitBase == null) return null;
        return battleTalkUnitBase->UldManager.NodeList == null ? null : battleTalkUnitBase->UldManager.NodeList[0];
    }
        
        
}
