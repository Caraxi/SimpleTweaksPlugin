using System;
using System.Collections.Generic;
using System.Reflection;
using ImGuiNET;
using Lumina.Data;
using Lumina.Excel;

namespace SimpleTweaksPlugin.Debugging; 

public class LuminaDebugging : DebugHelper {
    private FieldInfo cacheField;
        
    public override void Draw() {
        if (ImGui.BeginTabBar(Name)) {
            if (ImGui.BeginTabItem($"Sheet Cache##luminaDebugging")) {
                cacheField ??= Service.Data.Excel.GetType().GetField("_sheetCache", BindingFlags.Instance | BindingFlags.NonPublic);

                if (cacheField == null) {
                    ImGui.Text("Missing Field: _sheetCache");
                    return;
                }
                    
                var cache = (Dictionary<Tuple<Language, ulong>, ExcelSheetImpl>) (cacheField.GetValue(Service.Data.Excel));
                if (ImGui.Button("Clear all Cache")) {
                    cache.Clear();
                }
                ImGui.Columns(4);

                Tuple<Language, ulong> delete = null;

                foreach (var kvp in cache) {
                    ImGui.Text($"{kvp.Key.Item1}");
                    ImGui.NextColumn();
                    ImGui.Text($"{kvp.Value.Name}");
                    ImGui.NextColumn();
                    ImGui.Text($"{kvp.Key.Item2:X}");
                    ImGui.NextColumn();
                    if (ImGui.SmallButton($"Remove##{kvp.Key.Item2}_{kvp.Key.Item1}_{kvp.Value.Name}")) {
                        delete = kvp.Key;
                    }
                    ImGui.NextColumn();
                }

                if (delete != null) {
                    cache.Remove(delete);
                }
            
                ImGui.Columns();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    public override string Name => "Lumina Debugging";
}