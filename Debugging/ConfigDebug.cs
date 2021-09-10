using System.Numerics;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class ConfigDebug : DebugHelper {
        public override string Name => "ConfigDebug";

        public override void Draw() {

            var config = Framework.Instance()->GetUiModule()->GetConfigModule();

            ImGui.Text("ConfigModule:");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong) config:X}");

            DebugManager.PrintOutObject(config);



            var oFields = config->GetOption(0)->GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            var vFields = config->GetValue(0)->GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);


            if (ImGui.BeginTable("optionsTable", 3 + oFields.Length + vFields.Length, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX, new Vector2(-1, -1))) {

                ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.WidthFixed, 90);

                foreach (var f in oFields) ImGui.TableSetupColumn($"{f.Name}");
                foreach (var f in vFields) ImGui.TableSetupColumn($"{f.Name}");

                ImGui.TableSetupScrollFreeze(1, 1);

                ImGui.TableHeadersRow();

                for (uint i = 0; i < ConfigModule.ConfigOptionCount; i++) {
                    var option = config->GetOption(i);
                    var value = config->GetValue(i);
                    ImGui.TableNextColumn();
                    ImGui.Text($"{i}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{i:X}");
                    ImGui.TableNextColumn();
                    DebugManager.ClickToCopyText($"{(ulong)option:X}");

                    foreach (var f in oFields) {
                        ImGui.TableNextColumn();
                        var v = f.GetValue(*option);
                        if (f.FieldType.IsPointer && v is Pointer p) {
                            var ptr = Pointer.Unbox(p);
                            ImGui.Text($"{(ulong)ptr:X}");
                        } else {
                            ImGui.Text($"{v}");
                        }
                    }
                    foreach (var f in vFields) {
                        ImGui.TableNextColumn();
                        var v = f.GetValue(*value);
                        if (f.FieldType.IsPointer && v is Pointer p) {
                            var ptr = Pointer.Unbox(p);
                            ImGui.Text($"{(ulong)ptr:X}");
                        } else {
                            ImGui.Text($"{v}");
                        }
                    }
                    ImGui.TableNextRow();
                }
                ImGui.EndTable();
            }











        }
    }
}
