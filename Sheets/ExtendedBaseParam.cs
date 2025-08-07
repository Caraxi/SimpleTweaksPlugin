using Dalamud.Bindings.ImGui;
using JetBrains.Annotations;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Sheets; 

[Sheet( "BaseParam")]
public readonly unsafe struct ExtendedBaseParam(ExcelPage page, uint offset, uint row) : IRowExtension<ExtendedBaseParam, BaseParam> {

    private const int ParamCount = 23;
    
    public BaseParam BaseParam => new(page, offset, row);
    public Collection<ushort> EquipSlotCategoryPct => new(page, offset, offset, &EquipSlotCategoryPctCtor, ParamCount);
    private static ushort EquipSlotCategoryPctCtor(ExcelPage page, uint parentOffset, uint offset, uint i) => i == 0 ? (ushort) 0 : page.ReadUInt16(offset + 8 + (i - 1) * 2);
    public static ExtendedBaseParam Create(ExcelPage page, uint offset, uint row) => new(page, offset, row);
    public uint RowId => row;


    #region Debug
    [UsedImplicitly]
    private static bool DebugSetup() {
        if (!ImGui.BeginTable("BaseParamDebug", 2 + ParamCount, ImGuiTableFlags.BordersH | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY)) return false;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Row");
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180);
        for (var i = 1U; i < ParamCount; i++) {
            ImGui.TableSetupColumn("%");
        }
        ImGui.TableHeadersRow();
        return true;
    }

    [UsedImplicitly]
    private void DebugShowRow() {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text($"{RowId}");
        ImGui.TableNextColumn();
        ImGui.Text($"{BaseParam.Name}");
        for (var i = 1; i < ParamCount; i++) {
            ImGui.TableNextColumn();
            ImGui.Text($"{EquipSlotCategoryPct[i]}");
        }
    }

    [UsedImplicitly]
    private static void DebugFinish() {
        ImGui.EndTable();
    }
    #endregion
}