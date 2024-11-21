using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using Lumina.Data;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

/* Heavily based on Namingway by Anna
 * https://git.anna.lgbt/ascclemens/Namingway/src/branch/main/Namingway/Renamer.cs
 */

[TweakName("Label Submarine Destinations with Letters")]
[TweakDescription("Uses the standard A-Z lettering to identify submarine destinations for easier use with other tools.")]
public unsafe class SubmarineDestinationLetters : UiAdjustments.SubTweak {
    private delegate nint GetSubmarineExplorationRowDelegate(uint explorationId);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 80 78 16 ?? 75 04", DetourName = nameof(GetExplorationRowDetour))]
    private HookWrapper<GetSubmarineExplorationRowDelegate> getSubmarineExplorationRowHook;

    private readonly Dictionary<uint, nint> allocations = new();
    private readonly Dictionary<uint, string> destinationNames = new();

    protected override void Enable() {
        destinationNames.Clear();
        var frSheet = Service.Data.Excel.GetSheet<SubmarineExploration>(Language.French);
        var sheet = Service.Data.Excel.GetSheet<SubmarineExploration>();
        foreach (var e in sheet) {
            var frRow = frSheet.GetRowOrDefault(e.RowId);
            if (frRow == null) continue;
            destinationNames.Add(e.RowId, $"{ToBoxedLetters(frRow.Value.Location.ExtractText())} {e.Destination.ToDalamudString().TextValue}");
        }
    }

    private nint GetExplorationRowDetour(uint statusId) {
        var data = getSubmarineExplorationRowHook.Original(statusId);

        try {
            return GetStatusSheetDetourInner(statusId, data);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Exception in GetStatusSheetDetour");
        }

        return data;
    }

    private nint GetStatusSheetDetourInner(uint statusId, nint data) {
        const int destinationOffset = 0;
        const int locationOffset = 4;
        const int expReward = 8;

        if (destinationNames.TryGetValue(statusId, out var name)) {
            if (allocations.TryGetValue(statusId, out var cached)) {
                return cached + destinationOffset;
            }

            if (data == nint.Zero) return data;
            var raw = new byte[1024];
            Marshal.Copy(data - destinationOffset, raw, 0, raw.Length);
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var oldName = ReadRawBytes(data + raw[destinationOffset]);
            var descBytes = ReadRawBytes(data + raw[locationOffset] + 4);
            var oldPost = raw[destinationOffset] + oldName.Length + 1 + descBytes.Length + 1;
            var newPost = raw[destinationOffset] + nameBytes.Length + 1 + descBytes.Length + 1;

            var newData = new byte[1536];

            // copy over header
            for (var i = 0; i < destinationOffset + raw[destinationOffset]; i++) {
                newData[i] = raw[i];
            }

            newData[destinationOffset] = raw[destinationOffset];
            newData[locationOffset] = (byte)(destinationOffset + newData[destinationOffset] + nameBytes.Length + 1 - 4);

            // copy icon
            for (var i = 0; i < 4; i++) {
                newData[expReward + i] = raw[expReward + i];
            }

            // copy name
            for (var i = 0; i < nameBytes.Length; i++) {
                newData[destinationOffset + newData[destinationOffset] + i] = nameBytes[i];
            }

            // copy description
            for (var i = 0; i < descBytes.Length; i++) {
                newData[locationOffset + newData[locationOffset] + i] = descBytes[i];
            }

            // copy post-description info
            for (var i = 0; i < raw.Length - oldPost; i++) {
                newData[newPost + i] = raw[oldPost + i];
            }

            var newSheet = Marshal.AllocHGlobal(newData.Length);
            Marshal.Copy(newData, 0, newSheet, newData.Length);

            allocations[statusId] = newSheet;
            return newSheet + destinationOffset;
        }

        return data;
    }

    protected override void Disable() {
        foreach (var ptr in allocations.Values) {
            Marshal.FreeHGlobal(ptr);
        }

        allocations.Clear();
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

    private static byte[] ReadRawBytes(nint ptr) {
        var bytes = new List<byte>();

        var bytePtr = (byte*)ptr;
        while (*bytePtr != 0) {
            bytes.Add(*bytePtr);
            bytePtr += 1;
        }

        return bytes.ToArray();
    }
}
