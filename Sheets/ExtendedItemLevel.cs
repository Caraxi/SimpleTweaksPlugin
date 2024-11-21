using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using JetBrains.Annotations;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Sheets; 

[Sheet( "ItemLevel")]
public readonly unsafe struct ExtendedItemLevel(ExcelPage page, uint offset, uint row) : IRowExtension<ExtendedItemLevel, ItemLevel> {
    private const int ParamCount = 75;
    // public readonly ushort[] BaseParam = new ushort[75];
    public RowRef<ItemLevel> ItemLevel => new(page.Module, row, page.Language);
    
    private static ushort BaseParamCtor(ExcelPage page, uint parentOffset, uint offset, uint i) => i == 0 ? (ushort) 0 : page.ReadUInt16(offset + (i - 1) * 2);

    public Collection<ushort> BaseParam => new Collection<ushort>(page, offset, offset, &BaseParamCtor, ParamCount);
    
    public static ExtendedItemLevel Create(ExcelPage page, uint offset, uint row) => new(page, offset, row);
    public uint RowId => row;
    
    #region Debug
    [UsedImplicitly]
    private static bool DebugSetup() {
        if (!ImGui.BeginTable("BaseParamDebug", 1 + ParamCount, ImGuiTableFlags.BordersH | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX)) return false;
        ImGui.TableSetupScrollFreeze(1, 1);
        ImGui.TableSetupColumn("ItemLevel");
        for (var i = 1U; i < ParamCount; i++) {
            if (Service.Data.GetExcelSheet<BaseParam>().TryGetRow(i, out var bp)) {
                ImGui.TableSetupColumn($"Max\n{bp.Name.ExtractText()}");
            } else {
                ImGui.TableSetupColumn($"Max\nBaseParam#{i}");
            }
            
        }
        ImGui.TableHeadersRow();
        return true;
    }

    [UsedImplicitly]
    private void DebugShowRow() {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text($"{RowId}");
        for (var i = 1; i < ParamCount; i++) {
            ImGui.TableNextColumn();
            ImGui.Text($"{BaseParam[i]}");
        }
    }

    [UsedImplicitly]
    private static void DebugFinish() {
        ImGui.EndTable();
    }
    #endregion
}
