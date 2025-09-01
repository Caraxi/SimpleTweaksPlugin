using System;
using System.Diagnostics;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Minimap Adjustments")]
[TweakDescription("Allows hiding elements of the minimap display.")]
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

        public float WeatherPosition;

        public Vector2 CoordinatesPosition;
    }

    public Configs Config { get; private set; }

    protected void DrawConfig(ref bool hasChanged) {
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

        if (hasChanged) Update(true);
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.ClientState.Login += OnLogin;
        base.Enable();
        Update(true);
    }

    [TerritoryChanged]
    private void OnTerritoryChanged(ushort _) {
        sw.Restart();
        Common.FrameworkUpdate -= WaitForUpdate;
        Common.FrameworkUpdate += WaitForUpdate;
    }

    protected override void Disable() {
        SaveConfig(Config);
        Common.FrameworkUpdate -= WaitForUpdate;
        Service.ClientState.Login -= OnLogin;
        base.Disable();
        Update(false);
    }

    private void OnLogin() {
        sw.Restart();
        Common.FrameworkUpdate -= WaitForUpdate;
        Common.FrameworkUpdate += WaitForUpdate;
    }

    private void WaitForUpdate() {
        try {
            if (!sw.IsRunning) sw.Restart();
            var unitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("_NaviMap").Address;
            if (unitBase == null) {
                if (sw.ElapsedMilliseconds > 30000) {
                    sw.Stop();
                    Common.FrameworkUpdate -= WaitForUpdate;
                }

                return;
            }

            Update(true);
            Common.FrameworkUpdate -= WaitForUpdate;
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            Common.FrameworkUpdate -= WaitForUpdate;
        }
    }

    public void Update(bool enabled) {
        var unitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("_NaviMap").Address;
        if (unitBase == null) return;

        if (unitBase->UldManager.NodeListCount < 19) return;

        var sunImage = unitBase->GetImageNodeById(16);
        if (enabled && Config.HideSun) sunImage->ToggleVisibility(false);
        else sunImage->ToggleVisibility(true);

        var weatherIcon = unitBase->GetComponentNodeById(14);
        if (enabled && Config.HideWeather) weatherIcon->ToggleVisibility(false);
        else weatherIcon->ToggleVisibility(true);

        if (enabled && !Config.HideWeather) {
            // Weather Position Set
            var rad = 95f;
            var x = 90 + rad * Math.Cos(Config.WeatherPosition + 5.51524f);
            var y = 90 + rad * Math.Sin(Config.WeatherPosition + 5.51524f);
            UiHelper.SetPosition(weatherIcon, (float)x, (float)y);
        } else {
            UiHelper.SetPosition(weatherIcon, 158, 24);
        }

        var standardBorderImage = unitBase->GetImageNodeById(15);
        if (enabled && Config.CleanBorder && Config.NoBorder) standardBorderImage->ToggleVisibility(false);
        else standardBorderImage->ToggleVisibility(true);

        var fancyBorderImage = unitBase->GetImageNodeById(13);
        if (enabled && Config.CleanBorder) fancyBorderImage->ToggleVisibility(false);
        else fancyBorderImage->ToggleVisibility(true);

        for (uint i = 9; i < 13; i++) {
            var directionIcon = unitBase->GetImageNodeById(i);
            if (enabled && Config.HideCompassDirections) directionIcon->ToggleVisibility(false);
            else directionIcon->ToggleVisibility(true);
        }

        var coordinateDisplay = unitBase->GetNodeById(5);
        if (enabled && Config.HideCoordinates) coordinateDisplay->ToggleVisibility(false);
        else coordinateDisplay->ToggleVisibility(true);
        if (enabled) {
            coordinateDisplay->SetPositionFloat(44 + Config.CoordinatesPosition.X, 194 + Config.CoordinatesPosition.Y);
        } else {
            coordinateDisplay->SetPositionFloat(44, 194);
        }

        var compassLockButton = unitBase->GetComponentNodeById(4);
        if (enabled && Config.HideCompassLock) compassLockButton->ToggleVisibility(false);
        else compassLockButton->ToggleVisibility(true);

        for (uint i = 2; i < 4; i++) {
            var zoomButton = unitBase->GetComponentNodeById(i);
            if (enabled && Config.HideZoom) zoomButton->ToggleVisibility(false);
            else zoomButton->ToggleVisibility(true);
        }
    }
}
