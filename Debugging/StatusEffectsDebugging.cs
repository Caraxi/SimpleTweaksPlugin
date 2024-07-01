using System;
using System.Linq;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class StatusEffectsDebugging : DebugHelper {
    public override string Name => "Status Effects Debugging";

    private void DrawStatusExplorer(BattleChara* battleChara) {
        var name = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(battleChara->Character.GameObject.GetName()));
        ImGui.Text($"{name.TextValue}  ");

        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)battleChara:X}");
        ImGui.SameLine();
        DebugManager.PrintOutObject(*battleChara, (ulong) battleChara);

        ImGui.Separator();

        var statusManager = battleChara->GetStatusManager();
        ImGui.Text($"Status Manager:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)statusManager:X}");
        ImGui.SameLine();
        DebugManager.PrintOutObject(*statusManager, (ulong) statusManager);
        ImGui.Separator();
        var status = battleChara->GetStatusManager()->Status.GetPointer(0);

        if (ImGui.BeginTable("statusTable", 7)) {

            ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Stack", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Object");


            ImGui.TableHeadersRow();

            var sheet = Service.Data.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Status>();

            for (var i = 0; i < 30; i++) {
                var s = sheet?.GetRow(status->StatusId);
                ImGui.TableNextColumn();
                ImGui.Text($"{i}");
                ImGui.TableNextColumn();
                ImGui.Text($"{status->StatusId}");
                ImGui.TableNextColumn();
                if (s != null) {
                    var statusName = s.Name.ToDalamudString().TextValue;
                    ImGui.Text($"{statusName}");
                }

                ImGui.TableNextColumn();
                ImGui.Text($"{status->StackCount}");
                ImGui.TableNextColumn();
                if (s is { IsPermanent: true }) {
                    ImGui.Text("Permanent");
                } else {
                    var ts = TimeSpan.FromSeconds(status->RemainingTime);
                    ImGui.Text($"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}");
                }

                ImGui.TableNextColumn();

                if (status->SourceId == 0xE0000000 || status->SourceId == 0) {
                    ImGui.Text("None");
                } else {
                    var sourceObj = CharacterManager.Instance()->LookupBattleCharaByEntityId(status->SourceId);
                    if (sourceObj == null) {
                        DebugManager.ClickToCopyText($"0x{status->SourceId:X}");
                    } else {
                        DebugManager.ClickToCopyText($"{sourceObj->Character.GameObject.NameString}", $"0x{status->SourceId:X}");
                    }
                }

                ImGui.TableNextColumn();
                DebugManager.PrintOutObject(*status, (ulong) status);
                status++;
            }

            ImGui.EndTable();
        }
    }

    private byte[] battleCharaKinds = { 1, 2 };

    public override void Draw() {

        if (ImGui.BeginTabBar($"statusExplorerTabs")) {
            if (ImGui.BeginTabItem($"Self")) {
                var selfObject = GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
                if (selfObject == null || selfObject->ObjectKind != ObjectKind.Pc) {
                    ImGui.Text("Self Not Found");
                } else {
                    DrawStatusExplorer((BattleChara*) selfObject);
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"Target")) {
                if (Service.Targets?.Target == null) {
                    ImGui.Text("No Target");
                } else {
                    var targetObject = (GameObject*) Service.Targets?.Target?.Address;
                    if (targetObject == null || !battleCharaKinds.Contains((byte)targetObject->ObjectKind)) {
                        ImGui.Text($"Unsupported Target Kind: {targetObject->ObjectKind}");
                    } else {
                        DrawStatusExplorer((BattleChara*) targetObject);
                    }
                }

                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem($"Focus Target")) {
                if (Service.Targets?.FocusTarget == null) {
                    ImGui.Text("No Focus Target");
                } else {
                    var targetObject = (GameObject*) Service.Targets?.FocusTarget?.Address;
                    if (targetObject == null || !battleCharaKinds.Contains((byte)targetObject->ObjectKind)) {
                        ImGui.Text($"Unsupported Target Kind: {targetObject->ObjectKind}");
                    } else {
                        DrawStatusExplorer((BattleChara*) targetObject);
                    }
                }

                ImGui.EndTabItem();
            }


            ImGui.EndTabBar();
        }


    }
}