﻿using System;
using System.Numerics;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.AtkArray;
using SimpleTweaksPlugin.AtkArray.NumberArrays;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class ArrayDataBrowser : DebugHelper {
    public override string Name => "Array Data";

    // Stealing another thing from aers to shove in my debugging menu and change it to my liking
    // based on https://github.com/aers/FFXIVAtkArrayDataBrowserPlugin/

    public enum ArrayType {
        Numbers,
        Strings,
    }

    private ArrayType selectedType = ArrayType.Numbers;
    private int selectedArray = -1;

    public static bool ContainsValue(NumberArrayData* array, int value) {
        for (var i = 0; i < array->AtkArrayData.Size; i++) {
            if (array->IntArray[i] == value) {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsValue(NumberArrayData* array, float value) {
        var floatArray = (float*)array->IntArray;
        for (var i = 0; i < array->AtkArrayData.Size; i++) {
            if (Math.Abs(floatArray[i] - value) < 0.1f) {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsValue(NumberArrayData* array, string value) {
        if (int.TryParse(value, out var intValue) && ContainsValue(array, intValue)) return true;
        if (float.TryParse(value, out var floatValue) && ContainsValue(array, floatValue)) return true;
        return false;
    }

    public static bool ContainsValue(StringArrayData* array, string value) {
        for (var i = 0; i < array->AtkArrayData.Size; i++) {
            var strPtr = array->StringArray[i];
            if (strPtr != null) {
                try {
                    var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(strPtr));
                    if (str.TextValue.ToLower().Contains(value.ToLower())) return true;
                } catch {
                    //
                }
            }
        }

        return false;
    }

    private string searchValue = string.Empty;

    public static void DrawArrayDataTable(NumberArrayData* array, NumberArray arrayHelper = null) {

        if (arrayHelper != null) {
            DebugManager.PrintOutObject(arrayHelper, (ulong) array);
            
            ImGui.Separator();
            if (!ImGui.CollapsingHeader("Number Table")) return;
        }
        
        
        if (ImGui.BeginTable("numbersTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Integer");
            ImGui.TableSetupColumn("Shorts");
            ImGui.TableSetupColumn("Bytes");
            ImGui.TableSetupColumn("Hex");
            ImGui.TableSetupColumn("Float");
            ImGui.TableHeadersRow();

            for (var i = 0; i < array->AtkArrayData.Size; i++) {
                ImGui.TableNextColumn();
                if (ImGui.GetIO().KeyShift) {
                    DebugManager.ClickToCopyText($"0x{(i*4).ToString("X").PadLeft((array->AtkArrayData.Size * 4).ToString().Length, '0')}", $"{(ulong)&array->IntArray[i]:X}");
                } else {
                    DebugManager.ClickToCopyText($"{i.ToString().PadLeft(array->AtkArrayData.Size.ToString().Length, '0')}", $"{(ulong)&array->IntArray[i]:X}");
                }
                
                ImGui.TableNextColumn();
                ImGui.Text($"{array->IntArray[i]}");
                ImGui.TableNextColumn();

                {
                    var a = (short*)&array->IntArray[i];
                    var w = ImGui.GetContentRegionAvail().X;
                    var bX = ImGui.GetCursorPosX();
                    for (var bi = 0; bi < 2; bi++) {
                        ImGui.SetCursorPosX(bX + (w / 2) * bi);
                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{a[bi]:X4}" : $"{a[bi]}");
                        if (bi != 1) ImGui.SameLine();
                    }
                }

                ImGui.TableNextColumn();

                {
                    var a = (byte*)&array->IntArray[i];
                    var w = ImGui.GetContentRegionAvail().X;
                    var bX = ImGui.GetCursorPosX();
                    for (var bi = 0; bi < 4; bi++) {
                        ImGui.SetCursorPosX(bX + (w / 4) * bi);
                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{a[bi]:X2}" : $"{a[bi]}");
                        if (bi != 3) ImGui.SameLine();
                    }
                }

                ImGui.TableNextColumn();

                var hexText = $"{array->IntArray[i]:X}";
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                ImGui.TextDisabled("00000000"[..(8 - hexText.Length)]);

                ImGui.SameLine();
                ImGui.PopStyleVar();
                ImGui.Text(hexText);
                ImGui.TableNextColumn();
                ImGui.Text($"{*(float*)(&array->IntArray[i])}");
            }

            ImGui.EndTable();
        }
        
    }

    public override void Draw() {
        var uiModule = Framework.Instance()->GetUIModule();
        if (uiModule == null) {
            ImGui.Text("UIModule unavailable. ");
            return;
        }

        ImGui.Text($"UIModule address - {(long)uiModule:X}");
        var atkArrayDataHolder = &uiModule->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
        ImGui.Text($"AtkArrayDataHolder address - {(long)atkArrayDataHolder:X}");
        ImGui.Text($"ExtendArrayData - array size: {atkArrayDataHolder->ExtendArrayCount} - array ptr: {(long)atkArrayDataHolder->ExtendArrays:X}");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);

        ImGui.BeginChild("arraySelect", new Vector2(230 * ImGui.GetIO().FontGlobalScale, -1), true);
        ImGui.InputTextWithHint("###valueSearchInput", "Search", ref searchValue, 128);

        if (ImGui.BeginTabBar("tabs")) {
            if (ImGui.BeginTabItem("Numbers")) {
                ImGui.BeginChild("arraySelectNumbers");

                for (var i = 0; i < atkArrayDataHolder->NumberArrayCount; i++) {
                    var array = atkArrayDataHolder->NumberArrays[i];
                    if (array == null) continue;
                    if (searchValue.Length > 0 && !ContainsValue(array, searchValue)) continue;

                    if (ArrayHelper.NumberArrays.TryGetValue(i, out var numberArrayHelper)) {
                        if (ImGui.Selectable($"{numberArrayHelper.Name} #{i.ToString().PadLeft(atkArrayDataHolder->NumberArrayCount.ToString().Length, '0')} [{array->AtkArrayData.Size}]", selectedArray == i && selectedType == ArrayType.Numbers)) {
                            selectedType = ArrayType.Numbers;
                            selectedArray = i;
                        }
                    } else {
                        if (ImGui.Selectable($"Number Array #{i.ToString().PadLeft(atkArrayDataHolder->NumberArrayCount.ToString().Length, '0')} [{array->AtkArrayData.Size}]", selectedArray == i && selectedType == ArrayType.Numbers)) {
                            selectedType = ArrayType.Numbers;
                            selectedArray = i;
                        }
                    }
                    
                    
                   
                }

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Strings")) {
                ImGui.BeginChild("arraySelectStrings");
                for (var i = 0; i < atkArrayDataHolder->StringArrayCount; i++) {
                    var array = atkArrayDataHolder->StringArrays[i];
                    if (array == null) continue;
                    if (searchValue.Length > 0 && !ContainsValue(array, searchValue)) continue;
                    if (ImGui.Selectable($"String Array #{i.ToString().PadLeft(atkArrayDataHolder->StringArrayCount.ToString().Length, '0')} [{array->AtkArrayData.Size}]", selectedArray == i && selectedType == ArrayType.Strings)) {
                        selectedType = ArrayType.Strings;
                        selectedArray = i;
                    }
                }

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("view", new Vector2(-1), true);

        if (selectedArray >= 0) {
            switch (selectedType) {
                case ArrayType.Numbers: {
                    
                    
                    ArrayHelper.NumberArrays.TryGetValue(selectedArray, out var numberArrayHelper);

                    if (numberArrayHelper != null) {
                        ImGui.Text(numberArrayHelper.Name);
                    } else {
                        ImGui.Text($"Number Array #{selectedArray.ToString().PadLeft(atkArrayDataHolder->NumberArrayCount.ToString().Length, '0')}");
                    }
                    
                    
                    var array = atkArrayDataHolder->NumberArrays[selectedArray];
                    if (array == null) {
                        ImGui.Text("Null");
                        break;
                    }

                    ImGui.Text("Address:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong)array:X}");

                    ImGui.Separator();
                    ImGui.BeginChild("numbersArrayView", new Vector2(-1));

                    DrawArrayDataTable(array, numberArrayHelper);

                    ImGui.EndChild();
                    break;
                }
                case ArrayType.Strings: {
                    ImGui.Text($"String Array #{selectedArray.ToString().PadLeft(atkArrayDataHolder->StringArrayCount.ToString().Length, '0')}");

                    var array = atkArrayDataHolder->StringArrays[selectedArray];

                    ImGui.Text("Address:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong)array:X}");

                    ImGui.Separator();
                    ImGui.BeginChild("stringArrayView", new Vector2(-1));

                    if (ImGui.BeginTable("stringsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {
                        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 50);
                        ImGui.TableSetupColumn("String");
                        ImGui.TableHeadersRow();

                        for (var i = 0; i < array->AtkArrayData.Size; i++) {
                            ImGui.TableNextColumn();
                            ImGui.Text($"{i.ToString().PadLeft(array->AtkArrayData.Size.ToString().Length, '0')}");
                            ImGui.TableNextColumn();

                            var strPtr = array->StringArray[i];
                            if (strPtr == null) {
                                ImGui.TextColored(new Vector4(1, 0, 0, 1), "string is null");
                            } else {
                                try {
                                    var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(strPtr));
                                    // ImGui.Text($"{str.TextValue}");

                                    foreach (var p in str.Payloads) {
                                        ImGui.Text($"{p}");
                                    }
                                } catch (Exception ex) {
                                    ImGui.TextColored(new Vector4(1, 0, 0, 1), ex.ToString());
                                }
                            }
                        }

                        ImGui.EndTable();
                    }

                    ImGui.EndChild();
                    break;
                }
            }
        }

        ImGui.EndChild();
    }
}
