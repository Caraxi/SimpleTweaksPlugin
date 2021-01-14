using System;
using Dalamud.Game.Internal;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public MinimapAdjustments.Configs MinimapAdjustments = new MinimapAdjustments.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class MinimapAdjustments : UiAdjustments.SubTweak {
        public class Configs {
            public bool HideCoordinates;
            public bool HideCompassLock;
            public bool HideCompassDirections;
            public bool HideSun;
            public bool CleanBorder;
            public bool NoBorder;
            public bool HideZoom;
            public bool HideWeather;
        }

        public Configs Config => PluginConfig.UiAdjustments.MinimapAdjustments;

        public override void DrawConfig(ref bool hasChanged) {
            if (!Enabled) 
                base.DrawConfig(ref hasChanged);
            else {
                if (ImGui.TreeNode($"{Name}")) {

                    hasChanged |= ImGui.Checkbox("Hide Coordinates", ref Config.HideCoordinates);
                    hasChanged |= ImGui.Checkbox("Hide Compass Lock", ref Config.HideCompassLock);
                    hasChanged |= ImGui.Checkbox("Hide Compass Directions", ref Config.HideCompassDirections);
                    hasChanged |= ImGui.Checkbox("Hide Zoom Buttons", ref Config.HideZoom);
                    hasChanged |= ImGui.Checkbox("Hide Sun", ref Config.HideSun);
                    hasChanged |= ImGui.Checkbox("Hide Weather", ref Config.HideWeather);
                    hasChanged |= ImGui.Checkbox("Clean Border", ref Config.CleanBorder);
                    if (Config.CleanBorder) {
                        ImGui.SameLine();
                        hasChanged |= ImGui.Checkbox("No Border", ref Config.NoBorder);
                    }

                    if (hasChanged) Update();
                    ImGui.TreePop();
                }
            }
        }

        public override string Name => "Minimap Adjustments";

        public override void Enable() {
            PluginInterface.ClientState.OnLogin += OnLogin;
            base.Enable();
            Update();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= WaitForUpdate;
            PluginInterface.ClientState.OnLogin -= OnLogin;
            base.Disable();
            Update();
        }

        
        private void OnLogin(object sender, EventArgs e) {
            PluginInterface.Framework.OnUpdateEvent += WaitForUpdate;
        }

        private void WaitForUpdate(Framework framework) {
            var unitBase = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (unitBase == null) return;
            Update();
            framework.OnUpdateEvent -= WaitForUpdate;
        }

        public void Update() {
            var unitBase = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (unitBase == null) return;

            if (unitBase->ULDData.NodeListCount < 19) return;
            
            var sunImage = unitBase->ULDData.NodeList[4];
            if (Enabled && Config.HideSun) UiHelper.Hide(sunImage); else UiHelper.Show(sunImage);
            
            var weatherIcon = unitBase->ULDData.NodeList[6];
            if (Enabled && Config.HideWeather) UiHelper.Hide(weatherIcon); else UiHelper.Show(weatherIcon);
            
            var standardBorderImage = unitBase->ULDData.NodeList[5];
            if (Enabled && Config.CleanBorder && Config.NoBorder) UiHelper.Hide(standardBorderImage); else UiHelper.Show(standardBorderImage);
            
            var fancyBorderImage = unitBase->ULDData.NodeList[8];
            if (Enabled && Config.CleanBorder) UiHelper.Hide(fancyBorderImage); else UiHelper.Show(fancyBorderImage);
            
            for (var i = 9; i < 13; i++) {
                var directionIcon = unitBase->ULDData.NodeList[i];
                if (Enabled && Config.HideCompassDirections) UiHelper.Hide(directionIcon); else UiHelper.Show(directionIcon);
            }
            
            var coordinateDisplay = unitBase->ULDData.NodeList[13];
            if (Enabled && Config.HideCoordinates) UiHelper.Hide(coordinateDisplay); else UiHelper.Show(coordinateDisplay);
            
            var compassLockButton = unitBase->ULDData.NodeList[16];
            if (Enabled && Config.HideCompassLock) UiHelper.Hide(compassLockButton); else UiHelper.Show(compassLockButton);
            
            for (var i = 17; i < 19; i++) {
                var zoomButton = unitBase->ULDData.NodeList[i];
                if (Enabled && Config.HideZoom) UiHelper.Hide(zoomButton); else UiHelper.Show(zoomButton);
            }
        }
    }
}
