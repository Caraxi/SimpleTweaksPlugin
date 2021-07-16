using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class BattleCharaDebug : DebugHelper {
        public override string Name => "Battle Character Debugging";

        public override void Draw() {
            var charaManager = SimpleTweaksPlugin.Client.CharacterManager;
            
            ImGui.Text("CharacterManager: ");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong)charaManager.Data:X}");
            
            ImGui.Indent();
            
            for (var i = 0; i < 100; i++) {
                var c = charaManager.BattleCharacter[i];
                if (c == null) continue;

                var name = Plugin.Common.ReadSeString(c->Character.GameObject.Name);
                
                ImGui.Text($"{(ulong)c:X}"); ImGui.SameLine();
                DebugManager.PrintOutObject(*c, (ulong) c, new List<string>(), false, $"[{i}] {name}");
            }
            ImGui.Unindent();
            
            
        }
    }
}

