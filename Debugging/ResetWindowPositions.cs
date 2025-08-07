using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace SimpleTweaksPlugin.Debugging;

public class ResetWindowPositions : DebugHelper {
    private List<string> windowList;
    private string searchInput = string.Empty;

    private string? resetWindowName;
    
    public override void Draw() {
        if (windowList == null) {
            var uiConfigFile = new FileInfo(Path.Join(Service.PluginInterface.ConfigFile.Directory?.Parent?.FullName ?? string.Empty, "dalamudUi.ini"));
            windowList = [];

            if (uiConfigFile.Exists) {
                var lines = File.ReadAllLines(uiConfigFile.FullName);
                foreach (var t in lines) {
                    if (!t.StartsWith("[Window][")) continue;
                    var windowName = t[9..^1];
                    windowList.Add(windowName);
                }

                windowList.Sort();
            }
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("###search", "Search...", ref searchInput, 512);

        if (ImGui.BeginChild("buttonScroll", ImGui.GetContentRegionAvail())) {
            var buttonIndex = 0;
            var dl = ImGui.GetWindowDrawList();
            foreach (var l in windowList) {
                if (!string.IsNullOrWhiteSpace(searchInput) && !l.Contains(searchInput, StringComparison.InvariantCultureIgnoreCase)) continue;
                if (ImGui.Button($"##Button#{buttonIndex++}", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2))) {
                    resetWindowName = l;
                    Service.PluginInterface.UiBuilder.Draw += DoReset;
                }

                dl.AddText(ImGui.GetItemRectMin() + ImGui.GetStyle().FramePadding, ImGui.GetColorU32(ImGuiCol.Text), l);
            }
        }

        ImGui.EndChild();
    }

    private void DoReset() {
        if (string.IsNullOrWhiteSpace(resetWindowName)) {
            Service.PluginInterface.UiBuilder.Draw -= DoReset;
            return;
        }

        ImGui.SetNextWindowPos(new Vector2(50, 50), ImGuiCond.Always);
        ImGui.SetNextWindowCollapsed(false);

        if (ImGui.Begin(resetWindowName, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs)) {
            Service.PluginInterface.UiBuilder.Draw -= DoReset;
            resetWindowName = null;
        }

        ImGui.End();
    }

    public override void Dispose() {
        Service.PluginInterface.UiBuilder.Draw -= DoReset;
    }

    public override string Name => "Reset Window Positions";
}
