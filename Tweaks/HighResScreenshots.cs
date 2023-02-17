using System;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class HighResScreenshots : Tweak {
    public override string Name => "High Resolution Screenshots";
    public override string Description => "Increases the resolution in game screenshots are taken at.";
    protected override string Author => "NotNite";
    public override bool Experimental => true;

    public class Configs : TweakConfig {
        public int Scale = 2;
        public float Delay = 1.0f;
        public bool HideDalamudUi;
    }

    public Configs Config { get; private set; }

    private delegate byte IsInputIDClickedDelegate(nint a1, int a2);

    private HookWrapper<IsInputIDClickedDelegate> isInputIDClickedHook;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.TextWrapped(
            "This tweak will increase the resolution of screenshots taken in game. It will NOT increase the scale of your HUD/plugin windows.");
        ImGui.TextWrapped("Your HUD will appear smaller while the screenshot is processing.");

        ImGui.NewLine();

        ImGui.TextWrapped("Higher scale will take longer and use more resources.");
        ImGui.TextWrapped(
            "The higher the scale is, the longer the delay lasts. Experiment with these settings to find the best options for your system.");

        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
        ImGui.TextWrapped("The game WILL crash if you set the scale too high.");
        ImGui.PopStyleColor();

        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 100);
        hasChanged |= ImGui.InputInt("Scale", ref Config.Scale);

        ImGui.SameLine();
        var device = Device.Instance();
        ImGui.TextDisabled($"{device->Width*Config.Scale}x{device->Height*Config.Scale}");
        
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 100);
        hasChanged |= ImGui.InputFloat("Delay", ref Config.Delay);

        if (Config.Scale < 2) Config.Scale = 2;
        if (Config.Delay < 0) Config.Delay = 0;
        hasChanged |= ImGui.Checkbox("Hide Dalamud UI in screenshots", ref Config.HideDalamudUi);
    };

    public override void Setup() {
        AddChangelogNewTweak("1.8.2.0");
        AddChangelog(Changelog.UnreleasedVersion, "Added option to hide dalamud UI for screenshot.");
        base.Setup();
    }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        isInputIDClickedHook ??=
            Common.Hook<IsInputIDClickedDelegate>("E9 ?? ?? ?? ?? 83 7F 44 02", IsInputIDClickedDetour);
        isInputIDClickedHook?.Enable();

        base.Enable();
    }

    private bool shouldPress;
    private uint oldWidth;
    private uint oldHeight;

    const int ScreenshotButton = 543;

    // IsInputIDClicked is called from Client::UI::UIInputModule.CheckScreenshotState, which is polled
    // We change the res when the button is pressed and tell it to take a screenshot the next time it is polled
    private byte IsInputIDClickedDetour(nint a1, int a2) {
        var orig = isInputIDClickedHook.Original(a1, a2);

        if (orig == 1 && a2 == ScreenshotButton && !shouldPress) {
            var device = Device.Instance();
            oldWidth = device->Width;
            oldHeight = device->Height;

            device->NewWidth = oldWidth * (uint)Config.Scale;
            device->NewHeight = oldHeight * (uint)Config.Scale;
            device->RequestResolutionChange = 1;

            Service.Framework.RunOnTick(() => {
                if (Config.HideDalamudUi) UIDebug.SetExclusiveDraw(() => { });
                shouldPress = true;
            }, delay: TimeSpan.FromSeconds(Config.Delay));

            return 0;
        }

        if (a2 == ScreenshotButton && shouldPress) {
            shouldPress = false;
            
            // Reset the res back to normal after the screenshot is taken
            Service.Framework.RunOnTick(() => {
                UIDebug.FreeExclusiveDraw();
                var device = Device.Instance();
                device->NewWidth = oldWidth;
                device->NewHeight = oldHeight;
                device->RequestResolutionChange = 1;
            }, delayTicks: 1);

            return 1;
        }

        return orig;
    }

    public override void Disable() {
        UIDebug.FreeExclusiveDraw();
        SaveConfig(Config);
        isInputIDClickedHook?.Disable();
        base.Disable();
    }
}
