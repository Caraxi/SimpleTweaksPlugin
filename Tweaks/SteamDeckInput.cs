using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.System.Input.SoftKeyboards;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Use Steam Floating Keyboard")]
[TweakAuthor("KazWolfe")]
[TweakDescription("Replaces the default Steam Virtual Keyboard with one that doesn't take over the screen.")]
public unsafe class SteamDeckInput : Tweak
{
    // undocumented valve shenanigans, scale factor for steam deck.
    // probably shouldn't need to be changed. probably?
    private const float KeyboardScaleValue = 1.5f;

    private nint inputInterfaceOffset;

    public override bool CanLoad => Framework.Instance()->IsSteamApiInitialized() &&
                                    Framework.Instance()->SteamApi->IsRunningOnSteamDeck();

    [TweakHook(typeof(SteamGamepadSoftKeyboard), nameof(SteamGamepadSoftKeyboard.OpenSoftKeyboard),
        nameof(OpenSteamSoftKeyboardDetour))]
    private HookWrapper<SoftKeyboardDeviceInterface.Delegates.OpenSoftKeyboard> openSteamSoftKeyboardHook = null!;

    protected override void Enable()
    {
        this.inputInterfaceOffset =
            Marshal.OffsetOf<AtkComponentTextInput>(nameof(AtkComponentTextInput.SoftKeyboardInputInterface));
        if (this.inputInterfaceOffset == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputInterfaceOffset),
                "Failed to get offset of SoftKeyboardInputInterface, cowardly refusing to load!");
        }

        base.Enable();
    }

    private bool OpenSteamSoftKeyboardDetour(SoftKeyboardDeviceInterface* softKeyboardDevice,
        SoftKeyboardDeviceInterface.SoftKeyboardInputInterface* inputInterfacePtr)
    {
        // these should never happen, but sanity checks are probably a good idea nonetheless.
        if (inputInterfacePtr == null) return false;
        if (!Framework.Instance()->IsSteamApiInitialized()) return false;

        if (!softKeyboardDevice->IsEnabled())
            return false;

        // I do not know how to do this better
        var atkTextInput = (AtkComponentTextInput*)(inputInterfacePtr - inputInterfaceOffset);
        var resNode = atkTextInput->AtkComponentInputBase.AtkComponentBase.AtkResNode;

        if (resNode == null)
        {
            // shouldn't be possible, but fall back for safety.
            return openSteamSoftKeyboardHook.Original(softKeyboardDevice, inputInterfacePtr);
        }

        var scaledX = (int)(resNode->ScreenX / KeyboardScaleValue);
        var scaledY = (int)(resNode->ScreenY / KeyboardScaleValue);

        SimpleLog.Debug(
            $"Opening FloatingGamepad, textfield @ ({scaledX}, {scaledY}) with size {resNode->Width} {resNode->Height}");
        Framework.Instance()->SteamApi->ShowFloatingGamepadTextInput(
            scaledX, scaledY,
            resNode->Width, resNode->Height
        );

        return true;
    }
}