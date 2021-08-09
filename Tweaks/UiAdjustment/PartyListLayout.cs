using System;
using System.Diagnostics;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Newtonsoft.Json;
using SimpleTweaksPlugin.Converter;
using SimpleTweaksPlugin.GameStructs.NumberArray;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
#if DEBUG
using SimpleTweaksPlugin.Debugging;
#endif

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class PartyListLayout : UiAdjustments.SubTweak {
        public override string Name => "Party List Layout";
        public override string Description => "Change the number of columns used in the party list.";
        public override bool Experimental => true;
        public readonly Configs DefaultConfig = new();

        private bool previewMode = false;
        private Stopwatch previewModeTimer = new Stopwatch();
        
        public class Configs : TweakConfig {
            
            public bool ReverseFill;
            public int Columns = 1;
            public int SlotWidth = 260;
            public int SlotHeight = 80;
            
            // Elements
            public ElementConfig BarHP = new();
            public TextElementConfig NumberHP = new() { Color = new Vector4(1), Glow = new Vector4(0x31/255f, 0x61/255f, 0x86/255f, 0xFF/255f)};
            public ElementConfig BarMP = new();
            public TextElementConfig NumberMP = new() { Color = new Vector4(1), Glow = new Vector4(0x31/255f, 0x61/255f, 0x86/255f, 0xFF/255f)};
            public ElementConfig BarOvershield = new();
            public ElementConfig IconOvershield = new();
            public TextElementConfig Name = new() { Color = new Vector4(1), Glow = new Vector4(0x31/255f, 0x61/255f, 0x86/255f, 0xFF/255f)};
            public ElementConfig Castbar = new();
            public ElementConfig ClassIcon = new();
            public ElementConfig Slot = new();
            public ElementConfig LeaderIcon = new();
            public TextElementConfig ChocoboTimer = new() { Color = new Vector4(1), Glow = new Vector4(0x31/255f, 0x61/255f, 0x86/255f, 0xFF/255f)};
            public TextElementConfig ChocoboTimerClockIcon = new() { Color = new Vector4(1), Glow = new Vector4(0x31/255f, 0x61/255f, 0x86/255f, 0xFF/255f)};
            
            public StatusEffectsConfig StatusEffects = new();
        }

        private string importError = string.Empty;
        private readonly Stopwatch errorStopwatch = new();

        public class ElementConfig {
            public bool Hide;
            public Vector2 Position = new(0);
            public Vector2 Scale = new(1);
            
            public virtual void Editor(string name, ref bool c, PartyListLayout l = null) {
                c |= ImGui.DragFloat2($"Position##{name}", ref Position);
                c |= ImGui.SliderFloat2($"Scale##{name}", ref Scale, 0, 5);
            }
        }
        
        private string GetConfigExport() {
            var json = JsonConvert.SerializeObject(Config, Formatting.None, new VectorJsonConverter());
            var compressedString = Util.Compress($"SimpleTweaks.{Key}::{json}");
            SimpleLog.Verbose($"Compressed Length: {compressedString.Length}");
            return Util.Base64Encode(compressedString);
        }

        private Configs ImportConfig(string b64) {
            try {
                var bytes = Util.Base64Decode(b64);
                var decompressedString = Util.DecompressString(bytes);

                if (!decompressedString.StartsWith($"SimpleTweaks.{Key}::", StringComparison.InvariantCultureIgnoreCase)) {
                    SimpleLog.Log("Incorrect Identifier");
                    SimpleLog.Log($"{decompressedString}");
                    return null;
                }

                var json = decompressedString.Substring($"SimpleTweaks.{Key}::".Length);
                var obj = JsonConvert.DeserializeObject<Configs>(json, new VectorJsonConverter());
                return obj;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
                importError = "Failed to import config. Invalid Config String";
                return null;
            }
        }
        
        public class TextElementConfig : ElementConfig {
            public Vector4 Color = new (1);
            public Vector4 Glow = new(1);

            public override void Editor(string name, ref bool c, PartyListLayout l = null) {
                base.Editor(name, ref c, l);
                c |= ImGui.ColorEdit4($"Color##{name}", ref Color);
                c |= ImGui.ColorEdit4($"Glow##{name}", ref Glow);
            }
        }
        
        public class StatusEffectsConfig : ElementConfig {

            public bool TwoLines;
            public bool Vertical;
            public bool ReverseFill;
            public Vector2 Separation = new(0);
            
            public override void Editor(string name, ref bool c, PartyListLayout l = null) {
                base.Editor(name, ref c, l);
                c |= ImGui.Checkbox($"Two Lines##{name}", ref TwoLines);
                if (TwoLines) {
                    ImGui.SameLine();
                    c |= ImGui.Checkbox($"{(Vertical?"Fill Rows First": "Fill Columns First")}##{name}", ref ReverseFill);
                }
                c |= ImGui.Checkbox($"Vertical##{name}", ref Vertical);

                c |= ImGui.SliderFloat2("Icon Separation", ref Separation, -100, 100);
                
                if (l != null) {
                    ImGui.Indent();
                    ImGui.Indent();
                    var dl = ImGui.GetWindowDrawList();
                    var p = ImGui.GetCursorScreenPos();
                    
                    var mX = 0;
                    var mY = 0;
                
                    for (var i = 0; i < 10; i++) {
                        var (xO, yO) = l.StatusSlotPositions[i];
                    
                        dl.AddRect(p + new Vector2(30 * xO, 40 * yO), p + new Vector2(30 * xO + 24, 40 * yO + 32), 0xFFFFFFFF);
                        dl.AddText(p + new Vector2(30 * xO + 3, 40 * yO), 0xFFFFFFFF, $"{i + 1}");
                    
                        if (xO > mX) mX = xO;
                        if (yO > mY) mY = yO;
                    }

                    ImGui.Dummy(new Vector2(30 * (mX + 1), 40 * (mY + 1)));
                    ImGui.Unindent();
                    ImGui.Unindent();
                }
                
            }
        }
        
        public Configs Config { get; private set; }

        private (byte x, byte y)[] statusSlotPositions;

        public (byte x, byte y)[] StatusSlotPositions {
            get{
                if (statusSlotPositions == null) {
                    statusSlotPositions = new (byte x, byte y)[10];
                    byte xO = 0;
                    byte yO = 0;
                    
                    for (var i = 0; i < 10; i++) {
                        statusSlotPositions[i] = (xO, yO);
                        if (Config.StatusEffects.ReverseFill) {
                            if (Config.StatusEffects.Vertical) {
                                xO++;
                                if (Config.StatusEffects.TwoLines && xO % 2 == 0) {
                                    xO = 0;
                                    yO++;
                                }
                            } else {
                                yO++;
                                if (Config.StatusEffects.TwoLines && yO % 2 == 0) {
                                    yO = 0;
                                    xO++;
                                }
                            }
                        } else {
                            if (Config.StatusEffects.Vertical) {
                                yO++;
                                if (Config.StatusEffects.TwoLines && yO == 5) {
                                    yO = 0;
                                    xO++;
                                }
                            } else {
                                xO++;
                                if (Config.StatusEffects.TwoLines && xO == 5) {
                                    xO = 0;
                                    yO++;
                                }
                            }
                        }
                    }
                }

                return statusSlotPositions;
            }
        }

        private void ElementConfigEditor(string name, ElementConfig eCfg, ref bool c) {
            ImGui.Text($"{name}: ");
            ImGui.SameLine();
            c |= ImGui.Checkbox($"Hide##{name}", ref eCfg.Hide);

            if (!eCfg.Hide) {
                ImGui.Indent();
                eCfg.Editor(name, ref c, this);
                ImGui.Unindent();
            }
        }
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool c) => {
            var p = ImGui.GetCursorPos();
            
            if (!string.IsNullOrEmpty(importError)) {
                if (!errorStopwatch.IsRunning) errorStopwatch.Restart();
                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionWidth() - (ImGui.CalcTextSize(importError).X + 5));
                ImGui.TextColored(new Vector4(1, 0, 0, 1), importError);
                if (errorStopwatch.ElapsedMilliseconds > 5000) {
                    importError = string.Empty;
                    errorStopwatch.Stop();
                }
            } else {
                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionWidth() - (105 * ImGui.GetIO().FontGlobalScale));
                if (ImGui.Button("Export")) {
                    var json = GetConfigExport();
                    ImGui.SetClipboardText(json);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Copy {Name} config to clipboard.");
                ImGui.SameLine();
                if (ImGui.Button("Import")) {
                    importError = string.Empty;
                    var json = ImGui.GetClipboardText();
                    var cfg = ImportConfig(json);
                    if (cfg != null) Config = cfg;
                    c = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Load {Name} config from clipboard.");
            }
            
            ImGui.SetCursorPos(p);
            
            ImGui.Text("Columns / Rows:");
            ImGui.Indent();
            c |= ImGui.Checkbox("Fill Rows First?", ref Config.ReverseFill);
            c |= ImGui.SliderInt($"{(Config.ReverseFill ? "Row" : "Column")} Count###columnCount", ref Config.Columns, 1, 8);
            ImGui.Unindent();
            
            ImGui.Text("Sizing:");
            ImGui.Indent();
            c |= ImGui.SliderInt("Width", ref Config.SlotWidth, 50, 500);
            c |= ImGui.SliderInt("Height", ref Config.SlotHeight, 5, 160);
            ImGui.Unindent();
            
            
            ImGui.Text("Elements:");
            ImGui.Indent();
            ElementConfigEditor("Name", Config.Name, ref c);
            ElementConfigEditor("Class Icon", Config.ClassIcon, ref c);
            ElementConfigEditor("HP Bar", Config.BarHP, ref c);
            ElementConfigEditor("HP Number", Config.NumberHP, ref c);
            ElementConfigEditor("MP Bar", Config.BarMP, ref c);
            ElementConfigEditor("MP Number", Config.NumberMP, ref c);
            ElementConfigEditor("Oversheild Bar", Config.BarOvershield, ref c);
            ElementConfigEditor("Oversheild Icon", Config.IconOvershield, ref c);
            ElementConfigEditor("Chocobo Timer", Config.ChocoboTimer, ref c);
            ElementConfigEditor("Chocobo Timer Clock Icon", Config.ChocoboTimerClockIcon, ref c);
            ElementConfigEditor("Castbar", Config.Castbar, ref c);
            ElementConfigEditor("Slot Number", Config.Slot, ref c);
            ElementConfigEditor("Leader Icon", Config.LeaderIcon, ref c);
            ElementConfigEditor("Status Effects", Config.StatusEffects, ref c);

            ImGui.Unindent();
            try {
                if (c) {
                    statusSlotPositions = null;
                    previewMode = true;
                    Update(Common.GetUnitBase<AddonPartyList>());
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            
        };

        private delegate void* PartyListOnUpdate(AddonPartyList* @this, void* numArrayData, void* stringArrayData);
        private HookWrapper<PartyListOnUpdate> partyListOnUpdateHook;
        
        
        public override void Enable() {
            statusSlotPositions = null;
            Config = LoadConfig<Configs>() ?? new Configs();
            partyListOnUpdateHook ??= Common.Hook<PartyListOnUpdate>("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B 7A 20", PartyListUpdateDetour, false);
            partyListOnUpdateHook?.Enable();
            try {
                
                Update(Common.GetUnitBase<AddonPartyList>());
                
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            base.Enable();
        }
        
        private void* PartyListUpdateDetour(AddonPartyList* @this, void* a2, void* a3) {
            var ret = partyListOnUpdateHook.Original(@this, a2, a3);
            try {
#if DEBUG
                PerformanceMonitor.Begin("PartyListLayout.Update");
#endif
                Update(@this);
#if DEBUG
                PerformanceMonitor.End("PartyListLayout.Update");
#endif
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            return ret;
        }

        public override void Disable() {
            partyListOnUpdateHook?.Disable();
            SaveConfig(Config);
            try {
                Update(Common.GetUnitBase<AddonPartyList>(), true);
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            base.Disable();
        }
        
        private void Update(AddonPartyList* partyList, bool reset = false) {

            if (previewMode) {
                if (!previewModeTimer.IsRunning) previewModeTimer.Restart();
                if (previewModeTimer.ElapsedMilliseconds > 5000) {
                    previewMode = false;
                    previewModeTimer.Stop();
                    previewModeTimer.Reset();
                }
            }
            
            if (partyList == null) return;
            
            var atkArrayDataHolder = Common.UIModule->RaptureAtkModule.AtkModule.AtkArrayDataHolder;
            var partyListNumbers = atkArrayDataHolder.NumberArrays[4];
            var partyIntList = (AddonPartyListIntArray*) partyListNumbers->IntArray;
            if (partyList->AtkUnitBase.UldManager.NodeListSize < 17) return;
            var visibleIndex = 0;

            var maxX = 0;
            var maxY = 0;

            for (var i = 0; i < 13; i++) {
                try {
                    var pm = i switch {
                        >= 0 and <= 7 => partyList->PartyMember[i],
                        8 => partyList->Unknown08,
                        9 => partyList->Unknown09,
                        10 => partyList->Unknown10,
                        11 => partyList->Chocobo,
                        12 => partyList->Pet,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    var intList = i switch {
                        >= 0 and <= 7 => partyIntList->PartyMember[i],
                        _ => default
                    };
                    
                    var c = pm.PartyMemberComponent;
                    if (c == null) continue;
                    var cNode = c->OwnerNode;
                    if (cNode == null) continue;
                    
                    if (cNode->AtkResNode.IsVisible || reset) UpdateSlot(cNode, visibleIndex, pm, intList, ref maxX, ref maxY, reset);
                    if (cNode->AtkResNode.IsVisible) visibleIndex++;

                    if (i == 11) {
                        partyList->MpBarSpecialResNode->SetPositionFloat(153 + cNode->AtkResNode.X, 60 + cNode->AtkResNode.Y);
                        var cTextNode = partyList->MpBarSpecialResNode->ChildNode;
                        HandleElementConfig(cTextNode, Config.ChocoboTimer, reset, defColor: DefaultConfig.ChocoboTimer.Color, defGlow: DefaultConfig.ChocoboTimer.Glow);
                        if (cTextNode != null) HandleElementConfig(cTextNode->PrevSiblingNode, Config.ChocoboTimerClockIcon, reset, defColor: DefaultConfig.ChocoboTimerClockIcon.Color, defGlow: DefaultConfig.ChocoboTimerClockIcon.Glow, defPosX: 18);
                    }

                    if (partyListNumbers->IntArray[2] == i && cNode->AtkResNode.IsVisible) {
                        if (reset) {
                            partyList->LeaderMarkResNode->SetPositionShort(0, 30);
                            HandleElementConfig(partyList->LeaderMarkResNode->ChildNode, Config.LeaderIcon, reset, defPosX: 0, defPosY: 40);
                        } else {
                            partyList->LeaderMarkResNode->SetPositionShort(0, 0);
                            HandleElementConfig(partyList->LeaderMarkResNode->ChildNode, Config.LeaderIcon, reset, defPosX: cNode->AtkResNode.X, defPosY: cNode->AtkResNode.Y + 30);
                        }
                    }
                } catch {
                    // 
                }
            }
            
            // Collision Node Update
            partyList->AtkUnitBase.UldManager.NodeList[1]->SetWidth(reset ? (ushort)500 : (ushort) maxX);
            partyList->AtkUnitBase.UldManager.NodeList[1]->SetHeight(reset ? (ushort)480 : (ushort) maxY);
            
            // Background Update
            partyList->AtkUnitBase.UldManager.NodeList[3]->ToggleVisibility(reset);
        }

        private ByteColor GetColor(Vector4 vector4) {
            return new ByteColor() {
                R = (byte) (vector4.X * 255),
                G = (byte) (vector4.Y * 255),
                B = (byte) (vector4.Z * 255),
                A = (byte) (vector4.W * 255)
            };
        }
        
        private void HandleElementConfig(AtkResNode* resNode, ElementConfig eCfg, bool reset, float defScaleX = 1f, float defScaleY = 1f, float defPosX = 0, float defPosY = 0, Vector4 defColor = default, Vector4 defGlow = default) {
            if (resNode == null) return;
            if (eCfg.Hide && !reset) {
                resNode->SetScale(0, 0);
            } else {
                resNode->SetScale(reset ? defScaleX : defScaleX * eCfg.Scale.X, reset ? defScaleY : defScaleY * eCfg.Scale.Y);
                resNode->SetPositionFloat(reset ? defPosX : defPosX + eCfg.Position.X, reset ? defPosY : defPosY + eCfg.Position.Y);

                if (eCfg is TextElementConfig tec && resNode->Type == NodeType.Text) {
                    var tn = (AtkTextNode*)resNode;
                    tn->TextColor = GetColor(reset ? defColor : tec.Color );
                    tn->EdgeColor = GetColor(reset ? defGlow : tec.Glow);
                }
                
            }
        }
        
        private void UpdateSlot(AtkComponentNode* cNode, int visibleIndex, AddonPartyList.PartyListMemberStruct memberStruct, AddonPartyListMemberIntArray intArray, ref int maxX, ref int maxY, bool reset, int? forceColumnCount = null) {
            var c = cNode->Component;
            if (c == null) return;
            c->UldManager.NodeList[0]->SetWidth(reset ? (ushort)366 : (ushort)Config.SlotWidth); // Collision Node
            c->UldManager.NodeList[1]->SetWidth(reset ? (ushort)367 : (ushort)(Config.SlotWidth + 1));
            c->UldManager.NodeList[2]->SetWidth(reset ? (ushort)320 : (ushort)(Config.SlotWidth - 46)); 
            c->UldManager.NodeList[3]->SetWidth(reset ? (ushort)320 : (ushort)(Config.SlotWidth - 46));
            
            c->UldManager.NodeList[0]->SetHeight(reset ? (ushort) 44 : (ushort)(Config.SlotHeight - 16));
            c->UldManager.NodeList[1]->SetHeight(reset ? (ushort) 69 : (ushort)(Config.SlotHeight + 9));
            c->UldManager.NodeList[2]->SetHeight(reset ? (ushort) 69 : (ushort)(Config.SlotHeight + 9));
            c->UldManager.NodeList[2]->SetHeight(reset ? (ushort) 48 : (ushort)(Config.SlotHeight - 12));
            
            // Elements
            var hpComponent = memberStruct.HPGaugeComponent;
            if (hpComponent != null) {
                try {
                    HandleElementConfig(hpComponent->UldManager.NodeList[0], Config.BarHP, reset);
                    HandleElementConfig(hpComponent->UldManager.NodeList[2], Config.NumberHP, reset, defPosX: 4, defPosY: 21, defColor: DefaultConfig.NumberHP.Color, defGlow: DefaultConfig.NumberHP.Glow);

                    var hpGauge = hpComponent->UldManager.NodeList[0]->GetComponent();
                    if (hpGauge != null) {
                        HandleElementConfig(hpGauge->UldManager.NodeList[10], Config.IconOvershield, reset, defPosX: 90, defPosY: 9);
                        hpGauge->UldManager.NodeList[8]->SetScale(reset ? 1 : 0, reset ? 1 : 0);
                        hpGauge->UldManager.NodeList[9]->SetScale(reset ? 1 : 0, reset ? 1 : 0);
                        HandleElementConfig(hpGauge->UldManager.NodeList[7], Config.BarOvershield, reset, defPosY: 8);
                    }

                } catch {
                    // 
                }
            }

            var mpComponent = memberStruct.MPGaugeBar;
            if (mpComponent != null) {
                try {
                    HandleElementConfig(mpComponent->AtkComponentBase.UldManager.NodeList[0], Config.BarMP, reset, defPosY: 16);
                    HandleElementConfig(mpComponent->AtkComponentBase.UldManager.NodeList[1], Config.BarMP, reset, defPosY: 16);
                    mpComponent->AtkComponentBase.UldManager.NodeList[2]->SetScale(reset ? 1 : 0, reset ? 1 : 0);
                    mpComponent->AtkComponentBase.UldManager.NodeList[3]->SetScale(reset ? 1 : 0, reset ? 1 : 0);
                    HandleElementConfig(mpComponent->AtkComponentBase.UldManager.NodeList[4], Config.NumberMP, reset, defPosX: 5, defPosY: 22, defColor: DefaultConfig.NumberMP.Color, defGlow: DefaultConfig.NumberMP.Glow);
                    HandleElementConfig(mpComponent->AtkComponentBase.UldManager.NodeList[5], Config.NumberMP, reset, defPosX: -17, defPosY: 21, defColor: DefaultConfig.NumberHP.Color, defGlow: DefaultConfig.NumberMP.Glow);
                } catch {
                    //
                }
            }
            
            HandleElementConfig((AtkResNode*) memberStruct.Name, Config.Name, reset, defPosX: 17);
            HandleElementConfig((AtkResNode*) memberStruct.ClassJobIcon, Config.ClassIcon, reset, defPosX: 24, defPosY: 18);
            c->UldManager.NodeList[4]->SetPositionFloat(memberStruct.ClassJobIcon->AtkResNode.X - 21 * (reset ? 1 : memberStruct.ClassJobIcon->AtkResNode.ScaleX), memberStruct.ClassJobIcon->AtkResNode.Y - 13 * (reset ? 1 : memberStruct.ClassJobIcon->AtkResNode.ScaleY));
            c->UldManager.NodeList[4]->SetScale(memberStruct.ClassJobIcon->AtkResNode.ScaleX, memberStruct.ClassJobIcon->AtkResNode.ScaleY);
            HandleElementConfig((AtkResNode*) memberStruct.GroupSlotIndicator, Config.Slot, reset);
            HandleElementConfig((AtkResNode*) memberStruct.CastingActionName, Config.Castbar, reset, defPosY: 10);
            HandleElementConfig((AtkResNode*) memberStruct.CastingProgressBar, Config.Castbar, reset, defPosX: 8, defPosY: 7, defScaleX: intArray.CastingPercent >= 0 ? intArray.CastingPercent / 100f : 1f);
            HandleElementConfig((AtkResNode*) memberStruct.CastingProgressBarBackground, Config.Castbar, reset);
            
            if (reset) {
                cNode->AtkResNode.SetPositionFloat(0, visibleIndex * 40);
            } else {

                int columnIndex;
                int rowIndex;
                var columnCount = forceColumnCount ?? Config.Columns;
                if (Config.ReverseFill) {
                    columnIndex = visibleIndex % columnCount;
                    rowIndex = visibleIndex / columnCount;
                } else {
                    rowIndex = visibleIndex % columnCount;
                    columnIndex = visibleIndex / columnCount;
                }

                cNode->AtkResNode.SetPositionFloat(columnIndex * Config.SlotWidth, rowIndex * Config.SlotHeight);
                
                var xM = (columnIndex + 1) * Config.SlotWidth;
                var yM = (rowIndex + 1) * Config.SlotHeight + 16;
                
                if (xM > maxX) maxX = xM;
                if (yM > maxY) maxY = yM;
            }

            for (byte si = 0; si < 10; si++) {
                var siComponent = memberStruct.StatusIcon[si];
                if (siComponent == null) continue;
                var itcNode = siComponent->AtkComponentBase.OwnerNode;
                if (itcNode == null) continue;
                
                var (xSlot, ySlot) = reset ? (si, (byte)0) : StatusSlotPositions[si];
                
                var x = 263 + xSlot * ((25 + (reset ? 0 : Config.StatusEffects.Separation.X)) * (reset ? 1 : Config.StatusEffects.Scale.X));
                var y = 12 + ySlot * ((38 + (reset ? 0 : Config.StatusEffects.Separation.Y)) * (reset ? 1 : Config.StatusEffects.Scale.Y));
                
                HandleElementConfig((AtkResNode*) itcNode, Config.StatusEffects, reset, defPosX: x, defPosY: y);

                if (previewMode && !itcNode->AtkResNode.IsVisible) {
                    var imageNode = (AtkImageNode*)itcNode->Component->UldManager.NodeList[1]; 
                    imageNode->LoadIconTexture(10205, 0);
                    imageNode->AtkResNode.ToggleVisibility(true);
                    itcNode->AtkResNode.ToggleVisibility(true);
                }
            }
        }
    }
}
