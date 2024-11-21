using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class StatusEffectsDebugging : DebugHelper {
    public override string Name => "Status Effects Debugging";

    private void DrawStatusExplorer(BattleChara* battleChara) {
        var name = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(battleChara->Character.GameObject.GetName()));
        ImGui.Text($"{name.TextValue}  ");

        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)battleChara:X}");
        ImGui.SameLine();
        DebugManager.PrintOutObject(*battleChara, (ulong)battleChara);

        ImGui.Separator();

        var statusManager = battleChara->GetStatusManager();
        ImGui.Text($"Status Manager:");
        ImGui.SameLine();
        DebugManager.ClickToCopy(statusManager);
        ImGui.SameLine();
        DebugManager.PrintOutObject(statusManager);
        ImGui.Separator();

        if (ImGui.BeginTable("statusTable", 7)) {
            ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Stack", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Object");

            ImGui.TableHeadersRow();

            var sheet = Service.Data.Excel.GetSheet<Status>();

            for (var i = 0; i < statusManager->Status.Length; i++) {
                var status = statusManager->Status.GetPointer(i);
                
                ImGui.TableNextColumn();
                ImGui.Text($"{i}");
                ImGui.TableNextColumn();
                ImGui.Text($"{status->StatusId}");
                ImGui.TableNextColumn();

                if (sheet.TryGetRow(status->StatusId, out var s)) {
                    var statusName = s.Name.ExtractText();
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
                        var sourceName = SeString.Parse(sourceObj->Name);
                        DebugManager.ClickToCopyText($"{sourceName.TextValue}", $"0x{status->SourceId:X}");
                    }
                }

                ImGui.TableNextColumn();
                DebugManager.PrintOutObject(status);
            }

            ImGui.EndTable();
        }
    }

    private readonly ObjectKind[] battleCharaKinds = [ObjectKind.Pc, ObjectKind.BattleNpc];

    public override void Draw() {
        if (ImGui.BeginTabBar($"statusExplorerTabs")) {
            if (ImGui.BeginTabItem($"Self")) {
                var selfObject = GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
                if (selfObject == null || selfObject->ObjectKind != ObjectKind.Pc) {
                    ImGui.Text("Self Not Found");
                } else {
                    DrawStatusExplorer((BattleChara*)selfObject);
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"Target")) {
                if (Service.Targets?.Target == null) {
                    ImGui.Text("No Target");
                } else {
                    var targetObject = (GameObject*)Service.Targets?.Target?.Address;
                    if (targetObject == null || !battleCharaKinds.Contains(targetObject->ObjectKind)) {
                        ImGui.Text($"Unsupported Target Kind: {targetObject->ObjectKind}");
                    } else {
                        DrawStatusExplorer((BattleChara*)targetObject);
                    }
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem($"Focus Target")) {
                if (Service.Targets?.FocusTarget == null) {
                    ImGui.Text("No Focus Target");
                } else {
                    var targetObject = (GameObject*)Service.Targets?.FocusTarget?.Address;
                    if (targetObject == null || !battleCharaKinds.Contains(targetObject->ObjectKind)) {
                        ImGui.Text($"Unsupported Target Kind: {targetObject->ObjectKind}");
                    } else {
                        DrawStatusExplorer((BattleChara*)targetObject);
                    }
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }
}
