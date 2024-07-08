using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dalamud;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Add Number Separators")]
[TweakDescription("Add separators for various numbers shown in the UI")]
[TweakAuthor("Anna")]
[TweakVersion(1)]
public unsafe class AddNumberSeparators : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public bool FlyText = true;
        public bool AbilityCost;
        public bool AbilityTooltip;
        public bool PartyList = true;
        public char? CustomSeparator;
    }

    public Configs Config { get; private set; }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("Add separators to damage/healing numbers", ref Config.FlyText);
        hasChanged |= ImGui.Checkbox("Add separators to party list HP", ref Config.PartyList);
        hasChanged |= ImGui.Checkbox("Add separators to ability costs on hotbars", ref Config.AbilityCost);
        hasChanged |= ImGui.Checkbox("Add separators to ability costs in tooltips", ref Config.AbilityTooltip);

        var custom = Config.CustomSeparator?.ToString() ?? string.Empty;
        if (ImGui.InputText("Custom separator", ref custom, 1)) {
            hasChanged = true;
            Config.CustomSeparator = string.IsNullOrEmpty(custom) ? null : custom[0];
            SetSeparator(Config.CustomSeparator);
        }

        if (hasChanged) {
            SaveConfig(Config);
            ConfigureInstructions();
        }
    }

    private static class Signatures {
        internal const string ShowFlyText = "E8 ?? ?? ?? ?? FF C7 41 D1 C7";
        internal const string SprintfNumber = "48 83 EC 28 44 8B C9";

        internal const string FlyTextStringify = "45 33 C0 C6 44 24 ?? ?? 8B D3 E8 ?? ?? ?? ?? 41 8B CF E8";
        internal const string HotbarManaStringify = "45 33 C0 44 88 64 24 ?? 44 88 64 24 ?? 8B 14 08 48 8B CE E8";
        internal const string PartyListStringify = "45 33 C0 C6 44 24 ?? ?? 8B D5 E8 ?? ?? ?? ?? EB";

        internal const string Separator = "44 0F B6 05 ?? ?? ?? ?? 45 84 C0 74 36 F6 87";
    }

    private Dictionary<nint, byte[]> OldBytes { get; } = new();
    private byte OriginalSeparator { get; set; }
    private nint SeparatorPtr { get; set; }

    private delegate void ShowFlyTextDelegate(nint addon, uint actorIndex, uint messageMax, nint numbers, int offsetNum, int offsetNumMax, nint strings, int offsetStr, int offsetStrMax, int a10);

    [TweakHook, Signature(Signatures.ShowFlyText, DetourName = nameof(ShowFlyTextDetour))]
    private HookWrapper<ShowFlyTextDelegate>? _showFlyTextHook;

    private delegate nint SprintfNumberDelegate(uint number);

    [TweakHook, Signature(Signatures.SprintfNumber, DetourName = nameof(SprintfNumberDetour))]
    private HookWrapper<SprintfNumberDelegate>? _sprintfNumberHook;

    private static readonly byte[] ThirdArgOne = [
        0x41, 0xB0, 0x01,
    ];

    public override void Setup() {
        if (Service.SigScanner.TryGetStaticAddressFromSig(Signatures.Separator, out var separatorPtr)) {
            SeparatorPtr = separatorPtr;
            OriginalSeparator = *(byte*) separatorPtr;
        }

        base.Setup();
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        ConfigureInstructions();
        SetSeparator(Config.CustomSeparator);
        base.Enable();
    }

    protected override void Disable() {
        RestoreAllBytes();

        if (SeparatorPtr != 0 && OriginalSeparator != 0) {
            *(byte*) SeparatorPtr = OriginalSeparator;
        }

        base.Disable();
    }

    private void SetSeparator(char? sep) {
        if (SeparatorPtr == 0) {
            return;
        }

        var separator = (byte?) sep ?? OriginalSeparator;
        if (separator == 0) {
            separator = (byte) ',';
        }

        *(byte*) SeparatorPtr = separator;
    }

    internal void ConfigureInstructions() {
        ConfigureInstruction(Signatures.FlyTextStringify, Config.FlyText);
        ConfigureInstruction(Signatures.HotbarManaStringify, Config.AbilityCost);
        ConfigureInstruction(Signatures.PartyListStringify, Config.PartyList);
    }

    private void ConfigureInstruction(string sig, bool enabled) {
        if (!Service.SigScanner.TryScanText(sig, out var ptr)) {
            return;
        }

        if (enabled) {
            ReplaceBytes(ptr);
        } else {
            RestoreBytes(ptr);
        }
    }

    private void ReplaceBytes(nint ptr) {
        if (OldBytes.ContainsKey(ptr)) {
            return;
        }

        SafeMemory.ReadBytes(ptr, ThirdArgOne.Length, out var oldBytes);
        SafeMemory.WriteBytes(ptr, ThirdArgOne);
        OldBytes[ptr] = oldBytes;
    }

    private void RestoreBytes(nint ptr) {
        if (!OldBytes.TryGetValue(ptr, out var oldBytes)) {
            return;
        }

        SafeMemory.WriteBytes(ptr, oldBytes);
        OldBytes.Remove(ptr);
    }

    private void RestoreAllBytes() {
        foreach (var ptr in OldBytes.Keys.ToList()) {
            RestoreBytes(ptr);
        }
    }

    private void ShowFlyTextDetour(nint addon, uint actorIndex, uint messageMax, nint numbers, int offsetNum, int offsetNumMax, nint strings, int offsetStr, int offsetStrMax, int a10) {
        _showFlyTextHook!.Original(addon, actorIndex, messageMax, numbers, offsetNum, offsetNumMax, strings, offsetStr, offsetStrMax, a10);

        if (!Config.FlyText) {
            return;
        }

        static void Action(nint ptr) {
            // only check text nodes
            var node = (AtkResNode*) ptr;
            if (node->Type != NodeType.Text) {
                return;
            }

            var text = (AtkTextNode*) node;
            var font = (text->AlignmentFontType & 0xF0) >> 4;
            // only touch text nodes with a font above four and less than eight
            if (font is not (> 4 and < 8)) {
                return;
            }

            // only touch text nodes with a string starting with a digit
            var stringPtr = text->NodeText.StringPtr;
            if (stringPtr == null || !char.IsDigit((char) *stringPtr)) {
                return;
            }

            // set the font type of the node to 4 for non-number support
            text->AlignmentFontType = (byte) ((text->AlignmentFontType & 0xF) | (4 << 4));
        }

        var unit = (AtkUnitBase*) addon;
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

        action((nint) node);

        if ((int) node->Type < 1000) {
            TraverseNodes(node->ChildNode, action);
        } else {
            var comp = (AtkComponentNode*) node;

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
        var ret = (byte*) _sprintfNumberHook!.Original(number);
        if (!Config.AbilityTooltip) {
            goto Return;
        }

        var nfi = (NumberFormatInfo) NumberFormatInfo.CurrentInfo.Clone();
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
        return (nint) ret;
    }
}
