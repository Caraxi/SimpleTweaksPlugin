using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;
using Dalamud.Bindings.ImGui;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class PartyDebugging : DebugHelper {
    public override string Name => "Party Debugging";

    public override void Draw() {
        var groupManager = GroupManager.Instance();

        DebugManager.ClickToCopyText($"{(ulong)groupManager:X}");
        ImGui.SameLine();

        DebugManager.PrintOutObject(*groupManager, (ulong)groupManager, new List<string>());

        if (groupManager->MainGroup.MemberCount < 1) {
            ImGui.Text("Not in a party");
        } else {
            ImGui.Text($"Party Member Count: {groupManager->MainGroup.MemberCount}");
            for (var i = 0; i < 8 && i < groupManager->MainGroup.PartyMembers.Length && i < groupManager->MainGroup.MemberCount; i++) {
                var partyMember = groupManager->MainGroup.PartyMembers.GetPointer(i);
                if (partyMember->EntityId == 0xE0000000) continue;
                var name = partyMember->NameString;

                DebugManager.ClickToCopy(partyMember);
                ImGui.SameLine();
                ImGui.Text($"{partyMember->NameString}");
                ImGui.SameLine();
                ImGui.Text($"Lv{partyMember->Level}");
                ImGui.SameLine();
                DebugManager.ClickToCopyText($"{partyMember->EntityId:X}");
                
                if (partyMember->EntityId != 0) {
                    var chara = GameObjectManager.Instance()->Objects.GetObjectByEntityId(partyMember->EntityId);
                    if (chara != null) {
                        ImGui.SameLine();
                        DebugManager.PrintOutObject(chara);
                    }
                }
                
                ImGui.Spacing();

                
            }
        }
    }
}
