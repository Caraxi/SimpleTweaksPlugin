using Lumina.Excel;

namespace SimpleTweaksPlugin.Sheets;

public interface IRowExtension<out TExtension, in TBase> : IExcelRow<TExtension> where TBase : struct, IExcelRow<TBase> where TExtension : struct, IExcelRow<TExtension>, IRowExtension<TExtension, TBase> {
    public static virtual TExtension GetExtended(IExcelRow<TBase> baseRow) {
        return Service.Data.GetExcelSheet<TExtension>().GetRow(baseRow.RowId);
    }
}
