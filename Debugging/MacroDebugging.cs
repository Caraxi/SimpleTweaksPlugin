using System;
using System.Numerics;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class MacroDebugging : DebugHelper {
    public override string Name => "Macros";

    private void DrawMacro(RaptureMacroModule.Macro* macro) {
        DebugManager.PrintAddress(macro);
        ImGui.SameLine();
        DebugManager.PrintOutObject(macro);
        ImGui.Text($"Icon: {macro->IconId} / {macro->MacroIconRowId}");
        foreach (var line in macro->Lines.PointerEnumerator()) {
            if (line == null) continue;
            ImGui.Text($"{line->ToString()}");
        }
    }

    private void DrawMacroPage(Span<RaptureMacroModule.Macro> span) {
        for (var i = 0; i < span.Length; i++) {
            var macro = span.GetPointer(i);
            if (macro == null) continue;
            if (macro->IconId == 0) continue;
            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(macro->IconId)).GetWrapOrDefault();

            if (icon != null) {
                ImGui.Image(icon.ImGuiHandle, new Vector2(24));
            } else {
                ImGui.Dummy(new Vector2(24));
            }

            ImGui.SameLine();
            if (ImGui.CollapsingHeader($"#{i:00} - {macro->Name.ToString()}")) {
                DrawMacro(macro);
            }

            if (i != 99) ImGui.Separator();
        }
    }

    public override void Draw() {
        var module = RaptureMacroModule.Instance();

        DebugManager.PrintAddress(module);
        ImGui.SameLine();
        DebugManager.PrintOutObject(module);

        if (ImGui.BeginTabBar("macroTabs")) {
            if (ImGui.BeginTabItem("Individual")) {
                DrawMacroPage(module->Individual);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Shared")) {
                DrawMacroPage(module->Shared);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }
}
