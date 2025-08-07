using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;

namespace SimpleTweaksPlugin.Debugging;

public class CustomSheetsDebugging : DebugHelper {
    private List<Type> types;

    public override void Draw() {
        if (types == null) {
            types = [];
            foreach (var t in Assembly.GetExecutingAssembly()
                         .GetTypes()) {
                var sheetAttr = t.GetCustomAttribute<SheetAttribute>();
                if (sheetAttr != null) types.Add(t);
            }
        }

        if (ImGui.BeginTabBar("customSheetsDebugging")) {
            foreach (var t in types) {
                if (ImGui.BeginTabItem($"{t.Name}##{t.FullName}")) {
                    ShowCustomSheet(t);

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
    }

    private void ShowCustomSheet(Type type) {
        var handleMethod = GetType()
            .GetMethod("ShowSheet", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (handleMethod != null) {
            var genericMethod = handleMethod.MakeGenericMethod(type);
            if (!pages.TryGetValue(type, out var p)) pages[type] = 0;
            genericMethod.Invoke(this, [pages[type]]);
        }
    }

    private Dictionary<Type, int> pages = new();

    private const int RowsPerPage = 100;

    private void ShowSheet<T>(int page = 0) where T : struct, IExcelRow<T> {
        var sheet = Service.Data.GetExcelSheet<T>();

        var maxPage = sheet.Count / RowsPerPage;

        ImGui.Text($"Rows: {sheet.Count}");

        if (sheet.Count > RowsPerPage) {
            using (ImRaii.Disabled(page == 0)) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft)) {
                    page--;
                    pages[typeof(T)] = page;
                }
            }

            ImGui.SameLine();

            if (ImGui.SliderInt("##PageNumber", ref page, 0, maxPage)) {
                if (page < 0) page = 0;
                if (page > maxPage) page = maxPage;
                pages[typeof(T)] = page;
            }

            ImGui.SameLine();
            using (ImRaii.Disabled(page == maxPage)) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight)) {
                    page++;
                    pages[typeof(T)] = page;
                }
            }
        }

        var r = typeof(T).GetMethod("DebugSetup", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.NonPublic)
            ?.Invoke(null, null);
        if (r is false) return;

        var printMethod = typeof(T).GetMethod("DebugShowRow", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var row in sheet.Skip(page * RowsPerPage)
                     .Take(RowsPerPage)) {
            if (printMethod != null) {
                printMethod.Invoke(row, null);
            } else {
                DebugManager.PrintOutObject(row, 0, [$"{typeof(T).Name}#{row.RowId}"], headerText: $"{typeof(T).Name}#{row.RowId}");
            }
        }

        typeof(T).GetMethod("DebugFinish", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.NonPublic)
            ?.Invoke(null, null);
    }

    public override string Name => "Custom Sheets";
}
