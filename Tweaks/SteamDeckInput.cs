using System;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
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
[TweakReleaseVersion("1.10.11.0")]
public unsafe class SteamDeckInput : Tweak
{
    private nint _inputInterfaceOffset;

    public override bool CanLoad => Framework.Instance()->IsSteamApiInitialized() &&
                                    Framework.Instance()->SteamApi->IsRunningOnSteamDeck();

    [TweakHook, Signature("48 83 EC 28 80 79 ?? ?? 74 ?? 48 85 D2", DetourName = nameof(OpenSteamSoftKeyboardDetour))]
    private HookWrapper<SoftKeyboardDeviceInterface.Delegates.OpenSoftKeyboard> _openSteamSoftKeyboardHook = null!;

    protected override void Enable()
    {
        
        this._inputInterfaceOffset =
            Marshal.OffsetOf<AtkComponentTextInput>(nameof(AtkComponentTextInput.SoftKeyboardInputInterface));
        if (this._inputInterfaceOffset == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_inputInterfaceOffset),
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
        var atkTextInput = (AtkComponentTextInput*)((nint)inputInterfacePtr - _inputInterfaceOffset);
        var resNode = atkTextInput->AtkResNode;

        if (resNode == null)
        {
            // shouldn't be possible, but fall back for safety.
            SimpleLog.Error("Failed to get ResNode from AtkComponentTextInput?!");
            return _openSteamSoftKeyboardHook.Original(softKeyboardDevice, inputInterfacePtr);
        }

        SimpleLog.Debug(
            $"Opening FloatingGamepad, textfield @ ({resNode->ScreenX}, {resNode->ScreenY}) with size {resNode->Width} {resNode->Height}");
        Framework.Instance()->SteamApi->ShowFloatingGamepadTextInput(
            (int)resNode->ScreenX, (int)resNode->ScreenY,
            resNode->Width, resNode->Height
        );

        return true;
    }
}
