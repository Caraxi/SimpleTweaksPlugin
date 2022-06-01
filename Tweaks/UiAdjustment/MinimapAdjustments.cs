using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class MinimapAdjustments : UiAdjustments.SubTweak {
    private Stopwatch sw = new();
        
    public class Configs : TweakConfig {
        public bool HideCoordinates;
        public bool HideCompassLock;
        public bool HideCompassDirections;
        public bool HideSun;
        public bool CleanBorder;
        public bool NoBorder;
        public bool HideZoom;
        public bool HideWeather;

        public float WeatherPosition = 0;

        public Vector2 CoordinatesPosition = new();
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox("Hide Coordinates", ref Config.HideCoordinates);
        if (!Config.HideCoordinates) {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300);
            hasChanged |= ImGui.DragFloat2("Position##CoordinatePosition", ref Config.CoordinatesPosition, 0.1f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("CTRL+Click to set exact value.");
            }
        }
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
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.ClientState.Login += OnLogin;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
        base.Enable();
        Update();
    }

    private void OnTerritoryChanged(object sender, ushort e) {
        sw.Restart();
        Service.Framework.Update -= WaitForUpdate;
        Service.Framework.Update += WaitForUpdate;
    }

    public override void Disable() {
        SaveConfig(Config);
        Service.Framework.Update -= WaitForUpdate;
        Service.ClientState.Login -= OnLogin;
        base.Disable();
        Update();
    }

        
    private void OnLogin(object sender, EventArgs e) {
        sw.Restart();
        Service.Framework.Update -= WaitForUpdate;
        Service.Framework.Update += WaitForUpdate;
    }

    private void WaitForUpdate(Framework framework) {
        try {
            if (!sw.IsRunning) sw.Restart();
            var unitBase = (AtkUnitBase*) Service.GameGui.GetAddonByName("_NaviMap", 1);
            if (unitBase == null) {
                if (sw.ElapsedMilliseconds > 30000) {
                    sw.Stop();
                    Service.Framework.Update -= WaitForUpdate;
                }
                return;
            }
            Update();
            Service.Framework.Update -= WaitForUpdate;
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            Service.Framework.Update -= WaitForUpdate;
        }
    }

    public void Update() {
        var unitBase = (AtkUnitBase*) Service.GameGui.GetAddonByName("_NaviMap", 1);
        if (unitBase == null) return;

        if (unitBase->UldManager.NodeListCount < 19) return;
            
        var sunImage = unitBase->UldManager.NodeList[4];
        if (Enabled && Config.HideSun) sunImage->ToggleVisibility(false); else sunImage->ToggleVisibility(true);
            
        var weatherIcon = unitBase->UldManager.NodeList[6];
        if (Enabled && Config.HideWeather) weatherIcon->ToggleVisibility(false); else weatherIcon->ToggleVisibility(true);
            
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
        if (Enabled && Config.CleanBorder && Config.NoBorder) standardBorderImage->ToggleVisibility(false); else standardBorderImage->ToggleVisibility(true);
            
        var fancyBorderImage = unitBase->UldManager.NodeList[8];
        if (Enabled && Config.CleanBorder) fancyBorderImage->ToggleVisibility(false); else fancyBorderImage->ToggleVisibility(true);
            
        for (var i = 9; i < 13; i++) {
            var directionIcon = unitBase->UldManager.NodeList[i];
            if (Enabled && Config.HideCompassDirections) directionIcon->ToggleVisibility(false); else directionIcon->ToggleVisibility(true);
        }
            
        var coordinateDisplay = unitBase->UldManager.NodeList[13];
        if (Enabled && Config.HideCoordinates) coordinateDisplay->ToggleVisibility(false); else coordinateDisplay->ToggleVisibility(true);
        if (Enabled) {
            coordinateDisplay->SetPositionFloat(44 + Config.CoordinatesPosition.X, 194 + Config.CoordinatesPosition.Y);
        } else {
            coordinateDisplay->SetPositionFloat(44, 194);
        }
            
        var compassLockButton = unitBase->UldManager.NodeList[16];
        if (Enabled && Config.HideCompassLock) compassLockButton->ToggleVisibility(false); else compassLockButton->ToggleVisibility(true);
            
        for (var i = 17; i < 19; i++) {
            var zoomButton = unitBase->UldManager.NodeList[i];
            if (Enabled && Config.HideZoom) zoomButton->ToggleVisibility(false); else zoomButton->ToggleVisibility(true);
        }
    }
}