using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SimpleTweaksPlugin.Tweaks;

[Changelog("1.9.7.1", "Re-added 'Use ReShade' option")]
[TweakName("Screenshot Improvements")]
[TweakDescription("Allows taking higher resolution screenshots, Hiding Dalamud & Game UIs and removing the copyright notice from screenshots.")]
[TweakAuthor("NotNite")]
[Changelog(UnreleasedVersion, "Fixed 'Remove Copyright Text' option.")]
public unsafe class HighResScreenshots : Tweak {
    private nint copyrightShaderAddress;

    public class Configs : TweakConfig {
        public int Scale = 2;
        public float Delay = 1.0f;
        public bool HideDalamudUi;
        public bool HideGameUi;
        public bool RemoveCopyright;

        public bool UseCustom;
        public int CustomWidth = 1920;
        public int CustomHeight = 1080;
        
        public bool UseReShade;
        public VirtualKey ReShadeMainKey = VirtualKey.SNAPSHOT;
        public bool ReShadeCtrl;
        public bool ReShadeShift;
        public bool ReShadeAlt;
    }

    public Configs Config { get; private set; }

    private delegate byte IsInputIDClickedDelegate(nint a1, int a2);

    private HookWrapper<IsInputIDClickedDelegate> isInputIDClickedHook;
    
    private delegate byte ReShadeKeyTest(byte* a1, uint a2, byte a3, byte a4, byte a5, byte a6);
    private HookWrapper<ReShadeKeyTest> reShadeKeyTestHook;

    private bool updatingReShadeKeybind;
    
    protected void DrawConfig(ref bool hasChanged) {
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

        hasChanged |= ImGui.Checkbox("Use Fixed Resolution", ref Config.UseCustom);

        if (Config.UseCustom) {

            hasChanged |= ImGui.InputInt("Width", ref Config.CustomWidth);
            hasChanged |= ImGui.InputInt("Height", ref Config.CustomHeight);

        } else {
            ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 100);
            hasChanged |= ImGui.InputInt("Scale", ref Config.Scale);

            ImGui.SameLine();
            var device = Device.Instance();
            ImGui.TextDisabled($"{device->Width*Config.Scale}x{device->Height*Config.Scale}");
        }
        
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
        
        if (ImGui.Checkbox("Use ReShade to take screenshot", ref Config.UseReShade)) {
            hasChanged = true;
        }
        
        if (Config.UseReShade) {
            ImGui.Indent();
            ImGui.Indent();

            ImGui.TextWrapped("Take a screenshot using your FFXIV screenshot keybind.\nReShade will be used to take the screenshot instead.");
            ImGui.Spacing();
            var keybindText = new List<string>();
            if (Config.ReShadeCtrl) keybindText.Add("CTRL");
            if (Config.ReShadeAlt) keybindText.Add("ALT");
            if (Config.ReShadeShift) keybindText.Add("SHIFT");
            keybindText.Add($"{Config.ReShadeMainKey.GetFancyName()}");
            
            ImGui.Text($"Current Keybind: {string.Join(" + ", keybindText)}");
            if (updatingReShadeKeybind) {
                var keyDown = Service.KeyState.GetValidVirtualKeys().FirstOrDefault(k => k is not (VirtualKey.CONTROL or VirtualKey.SHIFT or VirtualKey.MENU) && Service.KeyState[k], VirtualKey.NO_KEY);
                if (keyDown != VirtualKey.NO_KEY) {
                    updatingReShadeKeybind = false;
                    Config.ReShadeMainKey = keyDown;
                    Config.ReShadeAlt = ImGui.GetIO().KeyAlt;
                    Config.ReShadeShift = ImGui.GetIO().KeyShift;
                    Config.ReShadeCtrl = ImGui.GetIO().KeyCtrl;
                } else {
                    ImGui.TextColored(ImGuiColors.DalamudOrange, "Take a screenshot with ReShade to update the keybind.");
                }
            } else {
                if (ImGui.Button("Update Keybind")) {
                    updatingReShadeKeybind = true;
                }
            }
            
            ImGui.Unindent();
            ImGui.Unindent();
        }
    }

    protected override void Setup() {
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

        if (!Service.SigScanner.TryScanText("48 8B 56 30 45 33 C9", out copyrightShaderAddress)) {
            copyrightShaderAddress = 0;
        }

        isInputIDClickedHook ??=
            Common.Hook<IsInputIDClickedDelegate>("E9 ?? ?? ?? ?? 83 7F ?? ?? 0F 8F ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B CB", IsInputIDClickedDetour);
        isInputIDClickedHook?.Enable();

        base.Enable();
    }

    private bool shouldPress;
    private uint oldWidth;
    private uint oldHeight;
    private bool isRunning;

    const int ScreenshotButton = 546;
    public bool originalUiVisibility;
    byte[] originalCopyrightBytes;
    // IsInputIDClicked is called from Client::UI::UIInputModule.CheckScreenshotState, which is polled
    // We change the res when the button is pressed and tell it to take a screenshot the next time it is polled
    private byte IsInputIDClickedDetour(nint a1, int a2) {
        if (a2 == ScreenshotButton && Config.UseReShade && Framework.Instance()->WindowInactive) return 0;
        
        var orig = isInputIDClickedHook.Original(a1, a2);
        if (AgentModule.Instance()->GetAgentByInternalId(AgentId.Configkey)->IsAgentActive()) return orig;

        if (orig == 1 && a2 == ScreenshotButton && !shouldPress && !isRunning) {
            isRunning = true;
            var device = Device.Instance();
            oldWidth = device->Width;
            oldHeight = device->Height;

            if (Config.UseCustom) {
                var w = Math.Clamp((uint)Config.CustomWidth, 1280, ushort.MaxValue);
                var h = Math.Clamp((uint)Config.CustomHeight, 720, ushort.MaxValue);
                if (device->Width != w || device->Height != h) {
                    device->NewWidth = w;
                    device->NewHeight = h;
                    device->RequestResolutionChange = 1;
                }
            } else {
                if (Config.Scale > 1) {
                    device->NewWidth = oldWidth * (uint)Config.Scale;
                    device->NewHeight = oldHeight * (uint)Config.Scale;
                    device->RequestResolutionChange = 1;
                }
            }
            
            

            if (Config.HideGameUi) {
                var raptureAtkModule = Framework.Instance()->GetUIModule()->GetRaptureAtkModule();
                originalUiVisibility = !raptureAtkModule->RaptureAtkUnitManager.Flags.HasFlag(AtkUnitManagerFlags.UiHidden);
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
                    var raptureAtkModule = Framework.Instance()->GetUIModule()->GetRaptureAtkModule();
                    if (originalUiVisibility && raptureAtkModule->RaptureAtkUnitManager.Flags.HasFlag(AtkUnitManagerFlags.UiHidden)) {
                        raptureAtkModule->SetUiVisibility(true);
                    }
                }

                var device = Device.Instance();
                if (device->Width != oldWidth || device->Height != oldHeight) {
                    device->NewWidth = oldWidth;
                    device->NewHeight = oldHeight;
                    device->RequestResolutionChange = 1;
                }
            }, delayTicks: Config.UseReShade ? 10 : 1);

            Service.Framework.RunOnTick(() => {
                if (originalCopyrightBytes != null) {
                    ReplaceRaw(copyrightShaderAddress, originalCopyrightBytes);
                    originalCopyrightBytes = null;
                }
                isRunning = false;
            }, delayTicks: 60);
            
            if (Config.UseReShade) {
                if (Config.ReShadeCtrl) SendInput.KeyDown(VirtualKey.CONTROL);
                if (Config.ReShadeAlt) SendInput.KeyDown(VirtualKey.MENU);
                if (Config.ReShadeShift) SendInput.KeyDown(VirtualKey.SHIFT);
                SendInput.KeyDown(Config.ReShadeMainKey);
                
                Service.Framework.RunOnTick(() => {
                    if (Config.ReShadeCtrl) SendInput.KeyUp(VirtualKey.CONTROL);
                    if (Config.ReShadeAlt) SendInput.KeyUp(VirtualKey.MENU);
                    if (Config.ReShadeShift) SendInput.KeyUp(VirtualKey.SHIFT);
                    SendInput.KeyUp(Config.ReShadeMainKey);
                }, delayTicks: 1);
                
                return 0;
            }

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
