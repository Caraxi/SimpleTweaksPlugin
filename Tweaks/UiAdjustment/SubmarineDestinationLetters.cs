using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.Interop;
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
    private delegate void* UpdateRowDelegate(AddonAirShipExploration* addon, uint index, AtkResNode** rowNodes);

    [TweakHook, Signature("40 57 48 83 EC 40 0F B7 81 ?? ?? ?? ?? 49 8B F8", DetourName = nameof(UpdateRowDetour))]
    private HookWrapper<UpdateRowDelegate> updateRowHook;

    private readonly Dictionary<string, string> destinationLetters = new();

    protected override void Enable() {
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

    private void AddLettersToRow(AddonAirShipExploration* addon, uint index, AtkResNode** rowNodes) {
        try {
            if (addon->DestinationCount <= index) return;
            if (rowNodes == null || rowNodes[3] == null) return;
            var nameNode = rowNodes[3]->GetAsAtkTextNode();
            if (nameNode == null) return;

            var data = addon->Destinations.GetPointer((int)index);
            if (!destinationLetters.TryGetValue(data->LocationName.ExtractText(), out var letters) && !destinationLetters.TryGetValue(data->DestinationName.ExtractText(), out letters)) return;
            
            var sb = new SeStringBuilder();
            sb.Append($"{letters} ");
            sb.Append(nameNode->NodeText.GetSeString());
            nameNode->SetText(sb.Encode());
        } catch(Exception ex) {
            SimpleLog.Error(ex, "Error adding letters to submarine destination row.");
        }
    }
    private void* UpdateRowDetour(AddonAirShipExploration* addon, uint index, AtkResNode** rowNodes) {
        try {
            return updateRowHook.Original(addon, index, rowNodes);
        } finally {
            AddLettersToRow(addon, index, rowNodes);
        }
    }

    private static string ToBoxedLetters(string inString) {
        return inString.Aggregate(string.Empty, (current, chr) => current + chr switch {
            >= 'A' and <= 'Z' => (char)(SeIconChar.BoxedLetterA + (chr - 'A')),
            >= 'a' and <= 'z' => (char)(SeIconChar.BoxedLetterA + (chr - 'a')),
            _ => chr
        });
    }
}
