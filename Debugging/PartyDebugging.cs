using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using ImGuiNET;
using PartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class PartyDebugging : DebugHelper {
    public override string Name => "Party Debugging";

    private GroupManager* groupManager;
        
    public override void Draw() {

        if (groupManager == null) {
            groupManager = (GroupManager*) Service.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? 44 8B E7");
        }
            
        DebugManager.ClickToCopyText($"{(ulong) groupManager:X}"); ImGui.SameLine();
            
        DebugManager.PrintOutObject(*groupManager, (ulong) groupManager, new List<string>());


        if (groupManager->MemberCount < 1) {
            ImGui.Text("Not in a party");
        } else {
                
            ImGui.Text($"Party Member Count: {groupManager->MemberCount}");

            var partyMembers = (PartyMember*) groupManager->PartyMembers;

            for (var i = 0; i < 8 && i < groupManager->MemberCount; i++) {
                var partyMember = partyMembers[i];
                var name = Marshal.PtrToStringAnsi(new IntPtr(partyMember.Name));
                ImGui.Text($"[{(ulong)&partyMember:X}] Lv {partyMember.Level}, {partyMember.ObjectID:X}, {name}");

                PlayerCharacter chara = null;

                for (var a = 0; a < Service.Objects.Length; a += 2) {
                    var actor = Service.Objects[a];
                    if (actor == null) continue;
                    if ((uint)actor.ObjectId == partyMember.ObjectID && actor is PlayerCharacter pc) {
                        chara = pc;
                    }
                }

                if (chara != null) {
                    DebugManager.PrintOutObject(chara, (ulong) chara.Address.ToInt64(), new List<string>());
                }
                    


            }
                
                
                
        }
            
            
            

    }

        
}