using System;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
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

    private nint copyrightShaderAddress;

    public class Configs : TweakConfig {
        public int Scale = 2;
        public float Delay = 1.0f;
        public bool HideDalamudUi;
        public bool HideGameUi;
        public bool RemoveCopyright;
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

        if (Config.Scale < 1) Config.Scale = 1;
        if (Config.Delay < 0) Config.Delay = 0;
        hasChanged |= ImGui.Checkbox("Hide Dalamud UI in screenshots", ref Config.HideDalamudUi);
        hasChanged |= ImGui.Checkbox("Hide Game UI in screenshots", ref Config.HideGameUi);
        if (copyrightShaderAddress == 0) {
            var disableColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
            ImGui.PushStyleColor(ImGuiCol.Text, disableColor);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, disableColor);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, disableColor);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, disableColor);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, disableColor);
            var f = false;
            ImGui.Checkbox("Remove copyight text", ref f);
            ImGui.PopStyleColor(5);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Failed to locate address needed for this option.");
            }
        } else {
            hasChanged |= ImGui.Checkbox("Remove copyright text", ref Config.RemoveCopyright);
        }
    };

    public override void Setup() {
        AddChangelogNewTweak("1.8.2.0");
        AddChangelog("1.8.3.0", "Added option to hide dalamud UI for screenshot.");
        AddChangelog(Changelog.UnreleasedVersion, "Added option to hide game UI for screenshots.");
        AddChangelog(Changelog.UnreleasedVersion, "Added option to remove the FFXIV Copyright from screenshots.");
        base.Setup();
    }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        if (!Service.SigScanner.TryScanText("49 8B 57 30 45 33 C9", out copyrightShaderAddress)) {
            copyrightShaderAddress = 0;
        }
        
        isInputIDClickedHook ??=
            Common.Hook<IsInputIDClickedDelegate>("E9 ?? ?? ?? ?? 83 7F 44 02", IsInputIDClickedDetour);
        isInputIDClickedHook?.Enable();

        base.Enable();
    }

    private bool shouldPress;
    private uint oldWidth;
    private uint oldHeight;

    const int ScreenshotButton = 543;
    public bool originalUiVisibility;
    byte[] originalCopyrightBytes = null;
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

            if (Config.HideGameUi) {
                var raptureAtkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();
                originalUiVisibility = raptureAtkModule->IsUiVisible;
                if (originalUiVisibility) {
                    raptureAtkModule->IsUiVisible = false;
                }
            }
            
            Service.Framework.RunOnTick(() => {
                if (Config.HideDalamudUi) UIDebug.SetExclusiveDraw(() => { });
                shouldPress = true;
            }, delay: TimeSpan.FromSeconds(Config.Delay));

            return 0;
        }

        if (a2 == ScreenshotButton && shouldPress) {
            shouldPress = false;
            
            if (Config.RemoveCopyright && copyrightShaderAddress != 0 && originalCopyrightBytes == null) {
                originalCopyrightBytes = ReplaceRaw(copyrightShaderAddress, new byte[] { 0xEB, 0x54 });
            }
            
            // Reset the res back to normal after the screenshot is taken
            Service.Framework.RunOnTick(() => {
                UIDebug.FreeExclusiveDraw();
                if (Config.HideGameUi) {
                    var raptureAtkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();
                    if (originalUiVisibility && !raptureAtkModule->IsUiVisible) {
                        raptureAtkModule->IsUiVisible = true;
                    }
                }

                var device = Device.Instance();
                device->NewWidth = oldWidth;
                device->NewHeight = oldHeight;
                device->RequestResolutionChange = 1;
            }, delayTicks: 1);

            Service.Framework.RunOnTick(() => {
                if (originalCopyrightBytes != null) {
                    ReplaceRaw(copyrightShaderAddress, originalCopyrightBytes);
                    originalCopyrightBytes = null;
                }
            }, delayTicks: 60);
            

            return 1;
        }

        return orig;
    }

    private static byte[] ReplaceRaw(nint address, byte[] data)
    {
        var originalBytes = MemoryHelper.ReadRaw(address, data.Length);
        var oldProtection = MemoryHelper.ChangePermission(address, data.Length, MemoryProtection.ExecuteReadWrite);
        MemoryHelper.WriteRaw(address, data);
        MemoryHelper.ChangePermission(address, data.Length, oldProtection);
        return originalBytes;
    }
    
    public override void Disable() {
        UIDebug.FreeExclusiveDraw();
        SaveConfig(Config);
        isInputIDClickedHook?.Disable();
        base.Disable();
    }
}
