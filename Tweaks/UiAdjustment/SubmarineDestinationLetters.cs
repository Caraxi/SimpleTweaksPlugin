using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
using Lumina.Data;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Label Submarine Destinations with Letters")]
[TweakDescription("Uses the standard A-Z lettering to identify submarine destinations for easier use with other tools.")]
[TweakVersion(2)]
[Changelog("1.10.9.4", "Rewritten to fix issues, re-enabled tweak.")]
public unsafe class SubmarineDestinationLetters : UiAdjustments.SubTweak {
    private delegate void* UpdateRowDelegate(AddonAirShipExploration* a1, uint a2, AtkResNode** a3);

    [TweakHook, Signature("40 57 48 83 EC 40 0F B7 81 ?? ?? ?? ?? 49 8B F8", DetourName = nameof(UpdateRowDetour))]
    private HookWrapper<UpdateRowDelegate> updateRowHook;

    private readonly Dictionary<string, string> destinationLetters = new();

    protected override void Enable() {
        
        #if DEBUG
        // Nag
        if (Common.ClientStructsVersion > 5800) {
            Plugin.Error(TweakManager, this, new Exception("Should really update this tweak to use FFXIVClientStucts..."), true);
        }
        #endif
        
        destinationLetters.Clear();
        var frSheet = Service.Data.Excel.GetSheet<SubmarineExploration>(Language.French);
        var sheet = Service.Data.Excel.GetSheet<SubmarineExploration>();
        foreach (var e in sheet) {
            var frRow = frSheet.GetRowOrDefault(e.RowId);
            if (frRow == null) continue;
            destinationLetters.TryAdd(e.Destination.ExtractText(), $"{ToBoxedLetters(frRow.Value.Location.ExtractText())}");
            destinationLetters.TryAdd(e.Location.ExtractText(), $"{ToBoxedLetters(frRow.Value.Location.ExtractText())}");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x1208)]
    private struct AddonAirShipExploration {
        [FieldOffset(0x0000)] public AtkUnitBase AtkUnitBase;
        [FieldOffset(0x9E0)] public Destination Destinations; // 64
        [FieldOffset(0x11F8)] public ushort NumDestinations;
        [FieldOffset(0x11FC)] public ushort DisplayedDestinationIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    private struct Destination {
        [FieldOffset(0x00)] public CStringPointer DestinationName;
        [FieldOffset(0x08)] public CStringPointer LocationName;
        [FieldOffset(0x10)] public ushort X;
        [FieldOffset(0x12)] public ushort Y;
        [FieldOffset(0x14)] public uint RankReq;
        [FieldOffset(0x18)] public uint ExpReward;
        [FieldOffset(0x1E)] public ushort Stars;
    }

    private void* UpdateRowDetour(AddonAirShipExploration* a1, uint a2, AtkResNode** a3) {
        try {
            return updateRowHook.Original(a1, a2, a3);
        } finally {
            try {
                if (a1->NumDestinations > a2) {
                    if (a3 != null && a3[3] != null) {
                        var nameNode = a3[3]->GetAsAtkTextNode();
                        if (nameNode != null) {
                            // TODO: Use FFXIVClientStructs
                            var data = (Destination*)((ulong)a1 + 0x9E0 + 0x20 * a2);
                            if (destinationLetters.TryGetValue(data->LocationName.ExtractText(), out var letters) || destinationLetters.TryGetValue(data->DestinationName.ExtractText(), out letters)) {
                                var sb = new SeStringBuilder();
                                sb.Append($"{letters} ");
                                sb.Append(nameNode->NodeText.GetSeString());
                                nameNode->SetText(sb.Encode());
                            }
                        }
                    }
                }
            } catch {
                // 
            }
        }
    }

    private static string ToBoxedLetters(string inString) {
        var outString = string.Empty;

        foreach (var chr in inString) {
            if (chr >= 0x61 && chr <= 0x7A) {
                outString += (char)(SeIconChar.BoxedLetterA + (chr - 0x61));
            } else if (chr >= 0x41 && chr <= 0x5A) {
                outString += (char)(SeIconChar.BoxedLetterA + (chr - 0x41));
            } else {
                outString += chr;
            }
        }

        return outString;
    }
}
