using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dalamud;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Add Number Separators")]
[TweakDescription("Add separators for various numbers shown in the UI")]
[TweakAuthor("Anna")]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.2.0")]
[Changelog("1.10.3.0", "Fixed issue causing custom separators to be lost when logging in.")]
[Changelog("1.10.9.0", "Fixed flytext number separator not working.")]
public unsafe class AddNumberSeparators : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public bool FlyText = true;
        public bool AbilityCost;
        public bool AbilityTooltip;
        public bool PartyList = true;
        public char? CustomSeparator;
    }

    [TweakConfig] public Configs Config { get; protected set; }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("Add separators to damage/healing numbers", ref Config.FlyText);
        hasChanged |= ImGui.Checkbox("Add separators to party list HP", ref Config.PartyList);
        hasChanged |= ImGui.Checkbox("Add separators to ability costs on hotbars", ref Config.AbilityCost);
        hasChanged |= ImGui.Checkbox("Add separators to ability costs in tooltips", ref Config.AbilityTooltip);

        var custom = Config.CustomSeparator?.ToString() ?? string.Empty;
        if (ImGui.InputText("Custom separator", ref custom, 1, ImGuiInputTextFlags.CallbackAlways, cb => {
                cb->SelectionStart = 0;
                cb->SelectionEnd = 1;
                return 0;
            })) {
            hasChanged = true;
            Config.CustomSeparator = string.IsNullOrEmpty(custom) || !char.IsAscii(custom[0]) || char.IsControl(custom[0]) ? null : custom[0];
            SetSeparatorDetour();
        }

        if (hasChanged) ConfigureInstructions();
    }

    private static class Signatures {
        internal const string ShowFlyText = "E8 ?? ?? ?? ?? FF C7 41 D1 C7";
        internal const string SprintfNumber = "E8 ?? ?? ?? ?? EB 68 48 8B 03";
        internal const string FlyTextStringify = "45 33 C0 C6 44 24 ?? ?? 8B D7 E8 ?? ?? ?? ?? 41 8B CC";
        internal const string HotbarManaStringify = "45 33 C0 48 8B CE 44 88 64 24 ?? 42 8B 54 B8 ?? E8 ?? ?? ?? ?? EB 21";
        internal const string PartyListStringify = "45 33 C0 C6 44 24 20 00 41 8B D6 E8 ?? ?? ?? ?? 49 8B";
        internal const string Separator = "44 0F B6 05 ?? ?? ?? ?? 45 84 C0 74 36 F6 87";
        internal const string SetSeparatorCharacter = "E8 ?? ?? ?? ?? 48 8B 4F 18 48 8B 01 FF 50 18 48 8B C8";
    }

    private Dictionary<nint, byte[]> OldBytes { get; } = new();

    [Signature(Signatures.Separator, ScanType = ScanType.StaticAddress)]
    private nint SeparatorPtr { get; set; }

    private delegate void ShowFlyTextDelegate(nint addon, uint actorIndex, uint messageMax, nint numbers, int offsetNum, int offsetNumMax, nint strings, int offsetStr, int offsetStrMax, int a10);

    [TweakHook, Signature(Signatures.ShowFlyText, DetourName = nameof(ShowFlyTextDetour))]
    private HookWrapper<ShowFlyTextDelegate>? showFlyTextHook;

    private delegate nint SprintfNumberDelegate(uint number);

    [TweakHook, Signature(Signatures.SprintfNumber, DetourName = nameof(SprintfNumberDetour))]
    private HookWrapper<SprintfNumberDelegate>? sprintfNumberHook;

    private delegate byte SetSeparator(uint* languageCode);

    [TweakHook, Signature(Signatures.SetSeparatorCharacter, DetourName = nameof(SetSeparatorDetour))]
    private HookWrapper<SetSeparator> setSeparatorHook;

    private static readonly byte[] ThirdArgOne = [
        0x41, 0xB0, 0x01,
    ];

    protected override void AfterEnable() {
        ConfigureInstructions();
        SetSeparatorDetour();
    }

    protected override void Disable() {
        RestoreAllBytes();

        var languageCode = stackalloc uint[1];
        languageCode[0] = (uint)Service.ClientState.ClientLanguage;
        setSeparatorHook.Original(languageCode);

        if (SeparatorPtr != nint.Zero) SimpleLog.Verbose($"Set Separator Character - Value: '{(char)*(byte*)SeparatorPtr}' @ {DebugManager.GetAddressString(SeparatorPtr.ToPointer())}");
    }

    private byte SetSeparatorDetour(uint* languageCode = null) {
        if (languageCode == null) {
            var lc = stackalloc uint[1];
            lc[0] = (uint)Service.ClientState.ClientLanguage;
            return SetSeparatorDetour(lc);
        }

        try {
            var separator = (byte?)Config.CustomSeparator;
            if (SeparatorPtr == nint.Zero || separator is null or 0) return setSeparatorHook.Original(languageCode);
            return *(byte*)SeparatorPtr = separator.Value;
        } finally {
            if (SeparatorPtr != nint.Zero) SimpleLog.Verbose($"Set Separator Character - Value: '{(char)*(byte*)SeparatorPtr}' @ {DebugManager.GetAddressString(SeparatorPtr.ToPointer())}");
        }
    }

    internal void ConfigureInstructions() {
        ConfigureInstruction("Fly Text", Signatures.FlyTextStringify, Config.FlyText);
        ConfigureInstruction("Hotbar Resource Cost", Signatures.HotbarManaStringify, Config.AbilityCost);
        ConfigureInstruction("Party List HP", Signatures.PartyListStringify, Config.PartyList);
    }

    private void ConfigureInstruction(string name, string sig, bool enabled) {
        if (!Service.SigScanner.TryScanText(sig, out var ptr)) {
            SimpleLog.Error($"Couldn't find signature {sig} for {name}");
            Plugin.Error(this, new Exception($"Signature '{sig}' for '{name}' not found."), true);
            return;

        }
        if (enabled) {
            ReplaceBytes(ptr);
        } else {
            RestoreBytes(ptr);
        }
    }

    private void ReplaceBytes(nint ptr) {
        if (OldBytes.ContainsKey(ptr)) return;
        SafeMemory.ReadBytes(ptr, ThirdArgOne.Length, out var oldBytes);
        SafeMemory.WriteBytes(ptr, ThirdArgOne);
        OldBytes[ptr] = oldBytes;
    }

    private void RestoreBytes(nint ptr) {
        if (!OldBytes.TryGetValue(ptr, out var oldBytes)) return;
        SafeMemory.WriteBytes(ptr, oldBytes);
        OldBytes.Remove(ptr);
    }

    private void RestoreAllBytes() {
        foreach (var ptr in OldBytes.Keys.ToList()) {
            RestoreBytes(ptr);
        }
    }

    private void ShowFlyTextDetour(nint addon, uint actorIndex, uint messageMax, nint numbers, int offsetNum, int offsetNumMax, nint strings, int offsetStr, int offsetStrMax, int a10) {
        showFlyTextHook!.Original(addon, actorIndex, messageMax, numbers, offsetNum, offsetNumMax, strings, offsetStr, offsetStrMax, a10);

        if (!Config.FlyText) {
            return;
        }

        static void Action(nint ptr) {
            // only check text nodes
            var node = (AtkResNode*)ptr;
            if (node->Type != NodeType.Text) {
                return;
            }

            var text = (AtkTextNode*)node;
            var font = (text->AlignmentFontType & 0xF0) >> 4;
            // only touch text nodes with a font above four and less than eight
            if (font is not (> 4 and < 8)) {
                return;
            }

            // only touch text nodes with a string starting with a digit
            var stringPtr = text->NodeText.StringPtr;
            if (stringPtr.Value == null || !char.IsDigit((char)*stringPtr.Value)) {
                return;
            }

            // set the font type of the node to 4 for non-number support
            text->AlignmentFontType = (byte)((text->AlignmentFontType & 0xF) | (4 << 4));
        }

        var unit = (AtkUnitBase*)addon;
        if (unit->RootNode != null) {
            TraverseNodes(unit->RootNode, Action);
        }

        for (var i = 0; i < unit->UldManager.NodeListCount; i++) {
            var node = unit->UldManager.NodeList[i];
            TraverseNodes(node, Action);
        }
    }

    private void TraverseNodes(AtkResNode* node, Action<nint> action, bool siblings = true) {
        if (node == null) {
            return;
        }

        action((nint)node);

        if ((int)node->Type < 1000) {
            TraverseNodes(node->ChildNode, action);
        } else {
            var comp = (AtkComponentNode*)node;

            for (var i = 0; i < comp->Component->UldManager.NodeListCount; i++) {
                TraverseNodes(comp->Component->UldManager.NodeList[i], action);
            }
        }

        if (!siblings) {
            return;
        }

        var prev = node;
        while ((prev = prev->PrevSiblingNode) != null) {
            TraverseNodes(prev, action, false);
        }

        var next = node;
        while ((next = next->NextSiblingNode) != null) {
            TraverseNodes(next, action, false);
        }
    }

    private nint SprintfNumberDetour(uint number) {
        var ret = (byte*)sprintfNumberHook!.Original(number);
        if (!Config.AbilityTooltip) {
            goto Return;
        }

        var nfi = (NumberFormatInfo)NumberFormatInfo.CurrentInfo.Clone();
        if (Config.CustomSeparator != null) {
            nfi.NumberGroupSeparator = Config.CustomSeparator.ToString();
        }

        var str = number.ToString("N0", nfi);
        var strBytes = Encoding.UTF8.GetBytes(str);
        fixed (byte* bytesPtr = strBytes) {
            Buffer.MemoryCopy(bytesPtr, ret, 0x40, strBytes.Length);
        }

        *(ret + strBytes.Length) = 0;

        Return:
        return (nint)ret;
    }
}
