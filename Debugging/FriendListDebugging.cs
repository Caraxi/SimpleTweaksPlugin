
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class FriendListDebugging : DebugHelper {
    public override string Name => "Friend List";

    public override void Draw() {
        var agent = AgentFriendList.Instance();
        if (agent == null) return;

        DebugManager.PrintOutObject(agent);

        if (agent->InfoProxy == null) {
            ImGui.Separator();
            ImGui.TextDisabled("Friend list is not loaded.");
            return;
        }

        if (ImGui.BeginTable("friends", 7, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("ContentID");
            ImGui.TableSetupColumn("Job/Level");
            ImGui.TableSetupColumn("Location");
            ImGui.TableSetupColumn("Company");
            ImGui.TableSetupColumn("Languages");
            ImGui.TableSetupColumn("Data");
            ImGui.TableHeadersRow();
            
            for (var i = 0U; i < agent->Count; i++) {
                var friend = agent->GetFriend(i);
                if (friend == null) continue;
                
                ImGui.TableNextRow();

                var name = friend->NameString;
                ImGui.TableNextColumn();
                ImGui.Text(name);
                ImGui.TableNextColumn();
                ImGui.Text($"{friend->ContentId:X}");


                ImGui.TableNextColumn();
                var job = Service.Data.GetExcelSheet<ClassJob>()?.GetRow(friend->Job);
                if (job == null) {
                    ImGui.TextDisabled("Unknown");
                } else if (job.RowId != 0) {
                    ImGui.Text($"{job.Abbreviation.ToDalamudString().TextValue} ??");
                }
                
                ImGui.TableNextColumn();
                var location = Service.Data.GetExcelSheet<TerritoryType>()?.GetRow(friend->Location);
                if (location == null) {
                    ImGui.TextDisabled($"Unknown");
                } else {
                    ImGui.Text($"{location.PlaceName.Value?.Name.ToDalamudString().TextValue}");
                }
                
                ImGui.TableNextColumn();
                var fcTag = friend->FCTagString;
                ImGui.Text($"{friend->GrandCompany} {fcTag}");

                ImGui.TableNextColumn();
                
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1));
                var dl = ImGui.GetWindowDrawList();
                ImGui.TextColored(friend->Languages.HasFlag(LanguageMask.Jp) ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudGrey3, "J");
                if (friend->ClientLanguage == Language.Jp) dl.AddLine(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 1), ImGui.GetItemRectMax() + new Vector2(0, 1), 0xFFFFFFFF, 2);
                
                ImGui.SameLine();
                ImGui.TextColored(friend->Languages.HasFlag(LanguageMask.En) ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudGrey3, "E");
                if (friend->ClientLanguage == Language.En) dl.AddLine(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 1), ImGui.GetItemRectMax() + new Vector2(0, 1), 0xFFFFFFFF, 2);

                ImGui.SameLine();
                ImGui.TextColored(friend->Languages.HasFlag(LanguageMask.De) ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudGrey3, "D");
                if (friend->ClientLanguage == Language.De) dl.AddLine(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 1), ImGui.GetItemRectMax() + new Vector2(0, 1), 0xFFFFFFFF, 2);

                ImGui.SameLine();
                ImGui.TextColored(friend->Languages.HasFlag(LanguageMask.Fr) ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudGrey3, "F");
                if (friend->ClientLanguage == Language.Fr) dl.AddLine(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 1), ImGui.GetItemRectMax() + new Vector2(0, 1), 0xFFFFFFFF, 2);
                ImGui.PopStyleVar();
                
                ImGui.TableNextColumn();
                DebugManager.PrintOutObject(friend);
            }
            
            ImGui.EndTable();
        }
    }
}


