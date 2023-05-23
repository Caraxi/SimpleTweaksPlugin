using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Logging;
using Dalamud.Utility;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

/* Heavily based on Namingway by Anna
 * https://git.anna.lgbt/ascclemens/Namingway/src/branch/main/Namingway/Renamer.cs
 */

public unsafe class SubmarineDestinationLetters : UiAdjustments.SubTweak {
    public override string Name => "Label Submarine Destinations with Letters";
    public override string Description => "Uses the standard A-Z lettering to identify submarine destinations for easier use with other tools.";
    public override bool Experimental => true;

    private delegate IntPtr GetSubmarineExplorationRowDelegate(uint explorationId);
    private HookWrapper<GetSubmarineExplorationRowDelegate> getSubmarineExplorationRowHook;
    private readonly Dictionary<uint, IntPtr> allocations = new();
    private readonly Dictionary<uint, string> destinationNames = new();

    public override void Enable() {
        destinationNames.Clear();
        var jpSheet = Service.Data.Excel.GetSheet<SubmarineExploration>(Language.French);
        if (jpSheet == null) return;
        var sheet = Service.Data.Excel.GetSheet<SubmarineExploration>();
        if (sheet == null) return;
        foreach (var e in sheet) {
            var jpRow = jpSheet.GetRow(e.RowId);
            if (jpRow == null) continue;
            destinationNames.Add(e.RowId, $"{ToBoxedLetters(jpRow.Location.RawString)} {e.Destination.ToDalamudString().TextValue}");
        }
            
        this.getSubmarineExplorationRowHook ??= Common.Hook<GetSubmarineExplorationRowDelegate>("80 78 16 6E 75 04", this.GetExplorationRowDetour);
        this.getSubmarineExplorationRowHook?.Enable();
        base.Enable();
    }

    private IntPtr GetExplorationRowDetour(uint statusId) {
        var data = this.getSubmarineExplorationRowHook.Original(statusId);

        try {
            return this.GetStatusSheetDetourInner(statusId, data);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Exception in GetStatusSheetDetour");
        }

        return data;
    }
    
    private IntPtr GetStatusSheetDetourInner(uint statusId, IntPtr data) {
            const int destinationOffset = 0;
            const int locationOffset = 4;
            const int expReward = 8;

            if (destinationNames.TryGetValue(statusId, out var name)) {
                if (this.allocations.TryGetValue(statusId, out var cached)) {
                    return cached + destinationOffset;
                }

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
                newData[locationOffset] = (byte) (destinationOffset + newData[destinationOffset] + nameBytes.Length + 1 - 4);

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

                this.allocations[statusId] = newSheet;
                return newSheet + destinationOffset;
            }

            return data;
        }
    
    public override void Disable() {
        foreach (var ptr in this.allocations.Values) {
            Marshal.FreeHGlobal(ptr);
        }
        allocations.Clear();
        getSubmarineExplorationRowHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        getSubmarineExplorationRowHook?.Dispose();
        base.Dispose();
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
    
    internal static byte[] ReadRawBytes(IntPtr ptr) {
        var bytes = new List<byte>();

        var bytePtr = (byte*) ptr;
        while (*bytePtr != 0) {
            bytes.Add(*bytePtr);
            bytePtr += 1;
        }

        return bytes.ToArray();
    }
}
