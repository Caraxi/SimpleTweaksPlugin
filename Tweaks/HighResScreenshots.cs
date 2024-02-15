using System;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class HighResScreenshots : Tweak {
    public override string Name => "Screenshot Improvements";
    public override string Description => "Allows taking higher resolution screenshots, Hiding Dalamud & Game UIs and removing the copyright notice from screenshots.";
    protected override string Author => "NotNite";

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
    
    private delegate byte ReShadeKeyTest(byte* a1, uint a2, byte a3, byte a4, byte a5, byte a6);
    private HookWrapper<ReShadeKeyTest> reShadeKeyTestHook;

    private bool updatingReShadeKeybind = false;
    
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
        AddChangelog("1.8.5.0", "Added option to hide game UI for screenshots.");
        AddChangelog("1.8.5.0", "Added option to remove the FFXIV Copyright from screenshots.");
        AddChangelog("1.8.5.1", "Renamed from 'High Resolution Screenshots' to 'Screenshot Improvements'");
        AddChangelog("1.8.6.0", "Added experimental option to use ReShade for screenshots.");
        base.Setup();
    }

    protected override void Enable() {
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
    private bool isRunning;

    const int ScreenshotButton = 543;
    public bool originalUiVisibility;
    byte[] originalCopyrightBytes = null;
    // IsInputIDClicked is called from Client::UI::UIInputModule.CheckScreenshotState, which is polled
    // We change the res when the button is pressed and tell it to take a screenshot the next time it is polled
    private byte IsInputIDClickedDetour(nint a1, int a2) {
        var orig = isInputIDClickedHook.Original(a1, a2);

        if (orig == 1 && a2 == ScreenshotButton && !shouldPress && !isRunning) {
            isRunning = true;
            var device = Device.Instance();
            oldWidth = device->Width;
            oldHeight = device->Height;

            if (Config.Scale > 1) {
                device->NewWidth = oldWidth * (uint)Config.Scale;
                device->NewHeight = oldHeight * (uint)Config.Scale;
                device->RequestResolutionChange = 1;
            }

            if (Config.HideGameUi) {
                var raptureAtkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();
                originalUiVisibility = !raptureAtkModule->RaptureAtkUnitManager.Flags.HasFlag(RaptureAtkModuleFlags.UiHidden);
                if (originalUiVisibility) {
                    raptureAtkModule->SetUiVisibility(false);
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
                    if (originalUiVisibility && raptureAtkModule->RaptureAtkUnitManager.Flags.HasFlag(RaptureAtkModuleFlags.UiHidden)) {
                        raptureAtkModule->SetUiVisibility(true);
                    }
                }

                var device = Device.Instance();
                if (device->Width != oldWidth || device->Height != oldHeight) {
                    device->NewWidth = oldWidth;
                    device->NewHeight = oldHeight;
                    device->RequestResolutionChange = 1;
                }
            }, delayTicks: 1);

            Service.Framework.RunOnTick(() => {
                if (originalCopyrightBytes != null) {
                    ReplaceRaw(copyrightShaderAddress, originalCopyrightBytes);
                    originalCopyrightBytes = null;
                }
                isRunning = false;
            }, delayTicks: 60);
            

            return 1;
        }

        if (isRunning && a2 == ScreenshotButton) return 0;
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

    protected override void Disable() {
        UIDebug.FreeExclusiveDraw();
        SaveConfig(Config);
        isInputIDClickedHook?.Disable();
        base.Disable();
    }
}
