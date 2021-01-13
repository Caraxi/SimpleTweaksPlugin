using System;
using System.Collections.Generic;
using Lumina.Excel;
using Common = SimpleTweaksPlugin.Helper.Common;

namespace SimpleTweaksPlugin.Sheets {
    public class CustomSheet {
        
        public uint RowId { get; set; }
        public uint SubRowId { get; set; }
        
        public virtual void Populate(RowParser parser) {
            RowId = parser.Row;
            SubRowId = parser.SubRow;
        }

        private static readonly Dictionary<Type, ICustomSheetWrapper> Data = new Dictionary<Type, ICustomSheetWrapper>();
        
        public static CustomSheetWrapper<T> GetSheet<T>(bool noCache = false) where T : CustomSheet, new() {
            if (!noCache && Data.ContainsKey(typeof(T))) return (CustomSheetWrapper<T>)Data[typeof(T)];
            var wrapper = new CustomSheetWrapper<T>(noCache);
            if (!noCache) Data.Add(typeof(T), wrapper);
            return wrapper;
        }
    }

    public interface ICustomSheetWrapper {}
    public class CustomSheetWrapper<T> : ICustomSheetWrapper where T : CustomSheet, new() {
        
        public string SheetName { get; }

        private readonly ExcelSheetImpl rawSheet;
        private readonly bool noCache;

        private readonly Dictionary<(uint id, uint subid), T> cachedRows = new Dictionary<(uint id, uint subid), T>();
        
        public CustomSheetWrapper(bool noCache = false) {
            this.noCache = noCache;
            var attrs = typeof(T).GetCustomAttributes(typeof(SheetAttribute), false);
            if (attrs.Length == 0) throw new Exception("Custom sheet missing Sheet attribute");
            SheetName = ((SheetAttribute) attrs[0]).Name;
            rawSheet = Common.PluginInterface.Data.Excel.GetSheetRaw(SheetName);
        }

        public T GetRow(uint id, uint subId = uint.MaxValue) {
            if (!noCache && cachedRows.ContainsKey((id, subId))) return cachedRows[(id, subId)];
            var rowParser = rawSheet.GetRowParser(id, subId);
            var sheetRow = new T();
            sheetRow.Populate(rowParser);
            if (!noCache) cachedRows.Add((id, subId), sheetRow);
            return sheetRow;
        }
    }
}
