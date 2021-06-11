using System;
using System.Numerics;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class BattleTalkAdjustments : UiAdjustments.SubTweak {
        public override string Name => "Battle Talk Adjustments";
        public override string Description => "Allows moving of the dialogue box that appears in the middle of battles.";
        protected override string Author => "Chivalrik";

        public class Configuration : TweakConfig {
            public int OffsetX = 0;
            public int OffsetY = 0;
            public float Scale = 1;
        }

        public Configuration Config { get; private set; }

        private float originalPositionX = 0f;
        private float originalPositionY = 0f;
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool changed) => {
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            changed |= ImGui.InputInt("X Offset##battletalkadjustments_offsetPosition", ref Config.OffsetX, 1);
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            changed |= ImGui.InputInt("Y Offset##battletalkadjustments_offsetPosition", ref Config.OffsetY, 1);
            ImGui.SetNextItemWidth(200 * ImGui.GetIO().FontGlobalScale);
            changed |= ImGui.SliderFloat("##battletalkadjustments_Scale", ref Config.Scale, 0.01f, 3f, "Scale: %.2fx");
            
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
            ImGui.Text("NEW BATTLE TALK DIALOGUE BOX POSITION");
            ImGui.End();
            if (!changed) return;
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdate;
        };

        public override void Enable() {
            Config = LoadConfig<Configuration>() ?? new Configuration();
            PluginInterface.ClientState.OnLogin += OnLogin;
            PluginInterface.ClientState.OnLogout += OnLogout;
            if(PluginInterface.ClientState.LocalPlayer is not null) OnLogin(null!,null!);
            base.Enable();
        }
        
        public override void Disable() {
            PluginInterface.ClientState.OnLogin -= OnLogin;
            PluginInterface.ClientState.OnLogout -= OnLogout;
            OnLogout(null!,null!);
            SaveConfig(Config);
            base.Disable();
        }

        private void OnLogin(object sender, EventArgs e)
        {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdateSetup;
        }
        
        private void OnLogout(object sender, EventArgs e)
        {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdateSetup;
            ResetBattleTalk();
        }
        
        private void FrameworkOnUpdateSetup(Framework framework)
        {
            try {
                var battleTalkNode = GetBattleTalkNode();
                if (battleTalkNode == null) return;
                originalPositionX = battleTalkNode->X;
                originalPositionY = battleTalkNode->Y;
                PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdateSetup;
                PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdate;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
        private void FrameworkOnUpdate(Framework framework) {
            try {
                var battleTalkNode = GetBattleTalkNode();
                if (battleTalkNode == null) return;
                UiHelper.SetPosition(battleTalkNode, originalPositionX + Config.OffsetX, originalPositionY + Config.OffsetY);
                UiHelper.SetScale(battleTalkNode, Config.Scale);
                PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
        
        private void ResetBattleTalk()
        {
            var battleTalkNode = GetBattleTalkNode();
            if (battleTalkNode == null) return;
            UiHelper.SetPosition(battleTalkNode, originalPositionX, originalPositionY);
            UiHelper.SetScale(battleTalkNode, 1);
        }
        
        private static AtkResNode* GetBattleTalkNode() {
            var battleTalkUnitBase = Common.GetUnitBase("_BattleTalk");
            if (battleTalkUnitBase == null) return null;
            return battleTalkUnitBase->UldManager.NodeList == null ? null : battleTalkUnitBase->UldManager.NodeList[0];
        }
        
        
    }
}