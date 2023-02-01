﻿using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class HighResScreenshots : Tweak {
    public override string Name => "High Resolution Screenshots";
    public override string Description => "Increases the resolution in game screenshots are taken at.";
    protected override string Author => "NotNite";

    public class Configs : TweakConfig {
        public int Scale = 2;
        public int Delay = 5;
    }

    public Configs Config { get; private set; }

    private delegate byte IsInputIDClickedDelegate(nint a1, int a2);
    private HookWrapper<IsInputIDClickedDelegate> isInputIDClickedHook;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("This tweak will increase the resolution of screenshots taken in game. It will NOT increase the scale of your HUD/plugin windows.");
        ImGui.Text("Your HUD will appear smaller while the screenshot is processing.");
        
        ImGui.NewLine();
        
        ImGui.Text("Higher scale will take longer and use more resources.");
        ImGui.Text("The higher the scale is, the longer the delay lasts. Experiment with these settings to find the best options for your system.");
        
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 100);
        hasChanged |= ImGui.InputInt("Scale", ref Config.Scale);
        
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 100);
        hasChanged |= ImGui.InputInt("Delay", ref Config.Delay);

        if (Config.Scale < 2) Config.Scale = 2;
        if (Config.Delay < 0) Config.Delay = 0;
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        isInputIDClickedHook ??= Common.Hook<IsInputIDClickedDelegate>("E9 ?? ?? ?? ?? 83 7F 44 02", IsInputIDClickedDetour);
        isInputIDClickedHook?.Enable();

        base.Enable();
    }

    private int stage;
    private int delayTicks;
    private uint oldWidth;
    private uint oldHeight;
    
    const int ScreenshotButton = 543;
    
    // IsInputIDClicked is called from Client::UI::UIInputModule.CheckScreenshotState, which is polled
    // We split into three stages on press:
    // - change res
    // - wait for delay & take screenshot
    // - fix res
    private byte IsInputIDClickedDetour(nint a1, int a2) {
        var orig = isInputIDClickedHook.Original(a1, a2);
        
        if (orig == 1 && a2 == ScreenshotButton && stage == 0) {
            stage = 1;

            var device = Device.Instance();
            oldWidth = device->Width;
            oldHeight = device->Height;

            device->NewWidth = oldWidth * (uint)Config.Scale;
            device->NewHeight = oldHeight * (uint)Config.Scale;
            device->RequestResolutionChange = 1;

            return 0;
        }

        if (a2 == ScreenshotButton && stage == 1) {
            delayTicks++;
            if (delayTicks >= Config.Delay) {
                stage = 2;
                delayTicks = 0;

                return 1;
            }

            return 0;
        }

        if (a2 == ScreenshotButton && stage == 2) {
            stage = 0;

            var device = Device.Instance();
            device->NewWidth = oldWidth;
            device->NewHeight = oldHeight;
            device->RequestResolutionChange = 1;
        }

        return orig;
    }

    public override void Disable() {
        SaveConfig(Config);
        isInputIDClickedHook?.Disable();
        base.Disable();
    }
}
