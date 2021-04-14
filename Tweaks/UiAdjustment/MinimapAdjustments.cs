using System;
using System.Diagnostics;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
        private Stopwatch sw = new();
        
        public class Configs {
            public bool HideCoordinates;
            public bool HideCompassLock;
            public bool HideCompassDirections;
            public bool HideSun;
            public bool CleanBorder;
            public bool NoBorder;
            public bool HideZoom;
            public bool HideWeather;

            public float WeatherPosition = 0;
        }

        public Configs Config => PluginConfig.UiAdjustments.MinimapAdjustments;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Hide Coordinates", ref Config.HideCoordinates);
            hasChanged |= ImGui.Checkbox("Hide Compass Lock", ref Config.HideCompassLock);
            hasChanged |= ImGui.Checkbox("Hide Compass Directions", ref Config.HideCompassDirections);
            hasChanged |= ImGui.Checkbox("Hide Zoom Buttons", ref Config.HideZoom);
            hasChanged |= ImGui.Checkbox("Hide Sun", ref Config.HideSun);
            hasChanged |= ImGui.Checkbox("Hide Weather", ref Config.HideWeather);
            if (!Config.HideWeather) {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                hasChanged |= ImGui.SliderAngle("Position##weatherPosition", ref Config.WeatherPosition, 0, 360);
            }

            hasChanged |= ImGui.Checkbox("Clean Border", ref Config.CleanBorder);
            if (Config.CleanBorder) {
                ImGui.SameLine();
                hasChanged |= ImGui.Checkbox("No Border", ref Config.NoBorder);
            }

            if (hasChanged) Update();
        };

        public override string Name => "Minimap Adjustments";
        public override string Description => "Allows hiding elements of the minimap display.";

        public override void Enable() {
            PluginInterface.ClientState.OnLogin += OnLogin;
            PluginInterface.ClientState.TerritoryChanged += OnTerritoryChanged;
            base.Enable();
            Update();
        }

        private void OnTerritoryChanged(object sender, ushort e) {
            sw.Restart();
            PluginInterface.Framework.OnUpdateEvent -= WaitForUpdate;
            PluginInterface.Framework.OnUpdateEvent += WaitForUpdate;
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= WaitForUpdate;
            PluginInterface.ClientState.OnLogin -= OnLogin;
            base.Disable();
            Update();
        }

        
        private void OnLogin(object sender, EventArgs e) {
            sw.Restart();
            PluginInterface.Framework.OnUpdateEvent -= WaitForUpdate;
            PluginInterface.Framework.OnUpdateEvent += WaitForUpdate;
        }

        private void WaitForUpdate(Framework framework) {
            try {
                if (!sw.IsRunning) sw.Restart();
                var unitBase = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
                if (unitBase == null) {
                    if (sw.ElapsedMilliseconds > 30000) {
                        sw.Stop();
                        framework.OnUpdateEvent -= WaitForUpdate;
                    }
                    return;
                }
                Update();
                framework.OnUpdateEvent -= WaitForUpdate;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
                framework.OnUpdateEvent -= WaitForUpdate;
            }
        }

        public void Update() {
            var unitBase = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (unitBase == null) return;

            if (unitBase->UldManager.NodeListCount < 19) return;
            
            var sunImage = unitBase->UldManager.NodeList[4];
            if (Enabled && Config.HideSun) UiHelper.Hide(sunImage); else UiHelper.Show(sunImage);
            
            var weatherIcon = unitBase->UldManager.NodeList[6];
            if (Enabled && Config.HideWeather) UiHelper.Hide(weatherIcon); else UiHelper.Show(weatherIcon);
            
            if (Enabled && !Config.HideWeather) {
                // Weather Position Set
                var rad = 95f;
                var x = 90 + rad * Math.Cos(Config.WeatherPosition + 5.51524f);
                var y = 90 + rad * Math.Sin(Config.WeatherPosition + 5.51524f);
                UiHelper.SetPosition(weatherIcon, (float)x, (float)y);
            } else {
                UiHelper.SetPosition(weatherIcon, 158, 24);
            }

            var standardBorderImage = unitBase->UldManager.NodeList[5];
            if (Enabled && Config.CleanBorder && Config.NoBorder) UiHelper.Hide(standardBorderImage); else UiHelper.Show(standardBorderImage);
            
            var fancyBorderImage = unitBase->UldManager.NodeList[8];
            if (Enabled && Config.CleanBorder) UiHelper.Hide(fancyBorderImage); else UiHelper.Show(fancyBorderImage);
            
            for (var i = 9; i < 13; i++) {
                var directionIcon = unitBase->UldManager.NodeList[i];
                if (Enabled && Config.HideCompassDirections) UiHelper.Hide(directionIcon); else UiHelper.Show(directionIcon);
            }
            
            var coordinateDisplay = unitBase->UldManager.NodeList[13];
            if (Enabled && Config.HideCoordinates) UiHelper.Hide(coordinateDisplay); else UiHelper.Show(coordinateDisplay);
            
            var compassLockButton = unitBase->UldManager.NodeList[16];
            if (Enabled && Config.HideCompassLock) UiHelper.Hide(compassLockButton); else UiHelper.Show(compassLockButton);
            
            for (var i = 17; i < 19; i++) {
                var zoomButton = unitBase->UldManager.NodeList[i];
                if (Enabled && Config.HideZoom) UiHelper.Hide(zoomButton); else UiHelper.Show(zoomButton);
            }
        }
    }
}
