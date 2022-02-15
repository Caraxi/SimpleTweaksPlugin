using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public unsafe class JobGaugeAdjustments : UiAdjustments.SubTweak {
        public class HideAndOffsetConfig {
            public bool Hide;
            public int OffsetX;
            public int OffsetY;
        }
        
        public override string Name => "Job Gauge Adjustments";
        public override string Description => "Allows moving and hiding parts of simple job gauges";
        protected override string Author => "Tinuviel";
        public override IEnumerable<string> Tags => new[] { "parameter", "job", "bar" };
        public Configs Config { get; set; }
        public Configs DefaultConfig = new Configs();
        private uint? lastJob;
        private int? updateCount;

        //public Dictionary<uint>

        public class Configs : TweakConfig {
            public HideAndOffsetConfig PLDOathBar = new() { OffsetX  = 28, OffsetY = 4 };
            public HideAndOffsetConfig PLDOathBarText = new() { OffsetX  = 140, OffsetY = 10 };
            public HideAndOffsetConfig PLDIronWillIndicator = new() { OffsetX = 0, OffsetY = 0 };
            
            public HideAndOffsetConfig RDMWhiteManaBar = new() { OffsetX  = 0, OffsetY = 0 };
            public HideAndOffsetConfig RDMWhiteManaText = new() { OffsetX  = 114, OffsetY = -6 };
            public HideAndOffsetConfig RDMBlackManaBar = new() { OffsetX  = 0, OffsetY = 13 };
            public HideAndOffsetConfig RDMBlackManaText = new() { OffsetX  = 114, OffsetY = 40 };
            public HideAndOffsetConfig RDMStatusIndicator = new() { OffsetX  = 146, OffsetY = 20 };
            public HideAndOffsetConfig RDMManaStacks = new() { OffsetX  = 0, OffsetY = 42 };
            // TODO: Split RDM into more elements. SUB SELECTOR!
            
            public HideAndOffsetConfig MCHOverheatIcon = new() { OffsetX  = 0, OffsetY = 0 };
            public HideAndOffsetConfig MCHOverheatText = new() { OffsetX  = 19, OffsetY = -2 };
            public HideAndOffsetConfig MCHQueenIcon = new() { OffsetX  = 0, OffsetY = -27 };
            public HideAndOffsetConfig MCHQueenText = new() { OffsetX  = 54, OffsetY = -29 };
            public HideAndOffsetConfig MCHBatteryBar = new() { OffsetX  = 0, OffsetY = 0 };
            public HideAndOffsetConfig MCHBatteryText = new() { OffsetX  = 111, OffsetY = 6 };
            public HideAndOffsetConfig MCHHeatBar = new() { OffsetX  = 0, OffsetY = 26 };
            public HideAndOffsetConfig MCHHeatText = new() { OffsetX  = 111, OffsetY = 32 };

            public HideAndOffsetConfig MNKChakra1 = new() { OffsetX = 0, OffsetY = 0 }; // 1 > 17 > 18
            public HideAndOffsetConfig MNKChakra2 = new() { OffsetX = 18, OffsetY = 0 }; // 1 > 17 > 19
            public HideAndOffsetConfig MNKChakra3 = new() { OffsetX = 36, OffsetY = 0 }; // 1 > 17 > 20
            public HideAndOffsetConfig MNKChakra4 = new() { OffsetX = 54, OffsetY = 0 }; // 1 > 17 > 21
            public HideAndOffsetConfig MNKChakra5 = new() { OffsetX = 72, OffsetY = 0 }; // 1 > 17 > 22
            public HideAndOffsetConfig MNKText = new() { OffsetX = -10, OffsetY = 4 }; // 0 > 24 > 38
            public HideAndOffsetConfig MNKBeastChakra1 = new() { OffsetX = 8, OffsetY = 8 }; // 0 > 24 > 33 > 34
            public HideAndOffsetConfig MNKBeastChakra2 = new() { OffsetX = 38, OffsetY = 8 }; // 0 > 24 > 33 > 35
            public HideAndOffsetConfig MNKBeastChakra3 = new() { OffsetX = 68, OffsetY = 8 }; // 0 > 24 > 33 > 36
            public HideAndOffsetConfig MNKLunarNadi = new() { OffsetX = 0, OffsetY = 0 }; // 0 > 24 > 25 > 26
            public HideAndOffsetConfig MNKSolarNadi = new() { OffsetX = 20, OffsetY = 0 }; // 0 > 24 > 25 > 29
            
            public HideAndOffsetConfig WARDefiance = new() { OffsetX = 0, OffsetY = -2 };
            public HideAndOffsetConfig WARBeastBar = new() { OffsetX = 28, OffsetY = 4 };
            public HideAndOffsetConfig WARBarText = new() { OffsetX = 140, OffsetY = 10 };
            
            public HideAndOffsetConfig DRGDragonGaugeText = new() { OffsetX = 112, OffsetY = 6 };
            public HideAndOffsetConfig DRGDragonGauge = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig DRGGaze1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig DRGGaze2 = new() { OffsetX = 18, OffsetY = 0 };
            public HideAndOffsetConfig DRGMind1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig DRGMind2 = new() { OffsetX = 18, OffsetY = 0 };
            
            public HideAndOffsetConfig BRDSongBar = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig BRDSongName = new() { OffsetX = 40, OffsetY = -3 };
            public HideAndOffsetConfig BRDRepertoire1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig BRDRepertoire2 = new() { OffsetX = 20, OffsetY = 0 };
            public HideAndOffsetConfig BRDRepertoire3 = new() { OffsetX = 40, OffsetY = 0 };
            public HideAndOffsetConfig BRDRepertoire4 = new() { OffsetX = 60, OffsetY = 0 };
            public HideAndOffsetConfig BRDSongCountdown = new() { OffsetX = 158, OffsetY = 19 };
            public HideAndOffsetConfig BRDSoulVoiceText = new() { OffsetX = 92, OffsetY = 16 };
            public HideAndOffsetConfig BRDSoulVoiceBar = new() { OffsetX = 0, OffsetY = 12 };
            public HideAndOffsetConfig BRDMageCoda = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig BRDArmyCoda = new() { OffsetX = 0, OffsetY = 22 };
            public HideAndOffsetConfig BRDWandererCoda = new() { OffsetX = 0, OffsetY = 44 };
        }

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();

            UpdateCurrentJobBar(false, true);
            Service.Framework.Update += OnFrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            UpdateCurrentJobBar(true, true);
            Service.Framework.Update -= OnFrameworkUpdate;
            base.Disable();
        }

        // TODO: Genericize this for all usage. Why are we all making our own UI Elements?
        private bool VisibilityAndOffsetEditor(string label, ref HideAndOffsetConfig config, HideAndOffsetConfig defConfig) {
            var hasChanged = false;
            var positionOffset = 185 * ImGui.GetIO().FontGlobalScale;
            var resetOffset = 250 * ImGui.GetIO().FontGlobalScale;

            hasChanged |= ImGui.Checkbox(label, ref config.Hide);
            if (!config.Hide) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt($"##offsetX_{label}", ref config.OffsetX);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt($"Offset##offsetY_{label}", ref config.OffsetY);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##resetOffset_{label}")) {
                    config.OffsetX = defConfig.OffsetX;
                    config.OffsetY = defConfig.OffsetY;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }

            return hasChanged;
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            if (ImGui.CollapsingHeader(LocString("Paladin"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Oath Bar"), ref Config.PLDOathBar, DefaultConfig.PLDOathBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Oath Bar Text"), ref Config.PLDOathBarText, DefaultConfig.PLDOathBarText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Iron Will"), ref Config.PLDIronWillIndicator, DefaultConfig.PLDIronWillIndicator);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);
            
            if (ImGui.CollapsingHeader(LocString("Monk"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Chakra 1"), ref Config.MNKChakra1, DefaultConfig.MNKChakra1);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Chakra 2"), ref Config.MNKChakra2, DefaultConfig.MNKChakra2);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Chakra 3"), ref Config.MNKChakra3, DefaultConfig.MNKChakra3);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Chakra 4"), ref Config.MNKChakra4, DefaultConfig.MNKChakra4);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Chakra 5"), ref Config.MNKChakra5, DefaultConfig.MNKChakra5);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Text"), ref Config.MNKText, DefaultConfig.MNKText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Beast Chakra 1"), ref Config.MNKBeastChakra1, DefaultConfig.MNKBeastChakra1);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Beast Chakra 2"), ref Config.MNKBeastChakra2, DefaultConfig.MNKBeastChakra2);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Beast Chakra 3"), ref Config.MNKBeastChakra3, DefaultConfig.MNKBeastChakra3);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Lunar Nadi"), ref Config.MNKLunarNadi, DefaultConfig.MNKLunarNadi);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Solar Nadi"), ref Config.MNKSolarNadi, DefaultConfig.MNKSolarNadi);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);
            
            if (ImGui.CollapsingHeader(LocString("Warrior"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Beast Gauge"), ref Config.WARBeastBar, DefaultConfig.WARBeastBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Beast Gauge Text"), ref Config.WARBarText, DefaultConfig.WARBarText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Defiance"), ref Config.WARDefiance, DefaultConfig.WARDefiance);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);
            
            if (ImGui.CollapsingHeader(LocString("Dragoon"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide LotD Bar"), ref Config.DRGDragonGauge, DefaultConfig.DRGDragonGauge);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide LotD Bar Text"), ref Config.DRGDragonGaugeText, DefaultConfig.DRGDragonGaugeText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Gaze 1"), ref Config.DRGGaze1, DefaultConfig.DRGGaze1);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Gaze 2"), ref Config.DRGGaze2, DefaultConfig.DRGGaze2);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Firstmind 1"), ref Config.DRGMind1, DefaultConfig.DRGMind1);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Firstmind 2"), ref Config.DRGMind2, DefaultConfig.DRGMind2);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);
            
            if (ImGui.CollapsingHeader(LocString("Bard"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Song Bar"), ref Config.BRDSongBar, DefaultConfig.BRDSongBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Song Countdown"), ref Config.BRDSongCountdown, DefaultConfig.BRDSongCountdown);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Song Name"), ref Config.BRDSongName, DefaultConfig.BRDSongName);
                // TODO: Repertoire stacks changing in battle on configuration weirdness (preview? remove entirely?)
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Repertoire 1"), ref Config.BRDRepertoire1, DefaultConfig.BRDRepertoire1);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Repertoire 2"), ref Config.BRDRepertoire2, DefaultConfig.BRDRepertoire2);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Repertoire 3"), ref Config.BRDRepertoire3, DefaultConfig.BRDRepertoire3);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Repertoire 4"), ref Config.BRDRepertoire4, DefaultConfig.BRDRepertoire4);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Soul Voice Text"), ref Config.BRDSoulVoiceText, DefaultConfig.BRDSoulVoiceText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Soul Voice Bar"), ref Config.BRDSoulVoiceBar, DefaultConfig.BRDSoulVoiceBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Mage's Coda"), ref Config.BRDMageCoda, DefaultConfig.BRDMageCoda);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Army's Coda"), ref Config.BRDArmyCoda, DefaultConfig.BRDArmyCoda);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Wanderer's Coda"), ref Config.BRDWandererCoda, DefaultConfig.BRDWandererCoda);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);
            
            if (ImGui.CollapsingHeader(LocString("Red Mage"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide White Mana"), ref Config.RDMWhiteManaBar, DefaultConfig.RDMWhiteManaBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide White Mana Text"), ref Config.RDMWhiteManaText, DefaultConfig.RDMWhiteManaText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Black Mana"), ref Config.RDMBlackManaBar, DefaultConfig.RDMBlackManaBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Black Mana Text"), ref Config.RDMBlackManaText, DefaultConfig.RDMBlackManaText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Status Indicator"), ref Config.RDMStatusIndicator, DefaultConfig.RDMStatusIndicator);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Mana Stacks"), ref Config.RDMManaStacks, DefaultConfig.RDMManaStacks);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);
            
            if (ImGui.CollapsingHeader(LocString("Mechanist"))) {
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Heat"), ref Config.MCHHeatBar, DefaultConfig.MCHHeatBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Heat Text"), ref Config.MCHHeatText, DefaultConfig.MCHHeatText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Overheat"), ref Config.MCHOverheatIcon, DefaultConfig.MCHOverheatIcon);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Overheat Text"), ref Config.MCHOverheatText, DefaultConfig.MCHOverheatText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Battery"), ref Config.MCHBatteryBar, DefaultConfig.MCHBatteryBar);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Battery Text"), ref Config.MCHBatteryText, DefaultConfig.MCHBatteryText);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Queen"), ref Config.MCHQueenIcon, DefaultConfig.MCHQueenIcon);
                hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Queen Text"), ref Config.MCHQueenText, DefaultConfig.MCHQueenText);
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);

            // hasChanged |= ImGui.ColorEdit4(LocString("HP Bar Color"), ref Config.HpColor);
            // hasChanged |= ImGui.ColorEdit4(LocString("MP Bar Color"), ref Config.MpColor);
            // hasChanged |= ImGui.ColorEdit4(LocString("GP Bar Color"), ref Config.GpColor);
            // hasChanged |= ImGui.ColorEdit4(LocString("CP Bar Color"), ref Config.CpColor);

            if (!hasChanged) return;
            
            UpdateCurrentJobBar(false, true);
            SaveConfig(Config);
        };

        private void OnFrameworkUpdate(Framework framework) {
            try {
                // TODO: Check if memory leak on 
                var job = Service.ClientState.LocalPlayer?.ClassJob.Id;
                if (job == null)
                    return;

                if (updateCount.HasValue)
                    updateCount++;

                // After a job changes, the gauges do not update instantly. We must wait a bit. A few framework ticks should be good
                if (lastJob == job || updateCount is < 5)
                    return;

                if (updateCount.HasValue) {
                    UpdateCurrentJobBar(false, true, job);
                    lastJob = job;
                    updateCount = null;
                } else {
                    updateCount = 0;
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void UpdateCurrentJobBar(bool reset, bool preview, uint? job = null) {
            job ??= Service.ClientState.LocalPlayer?.ClassJob.Id;
            if (job == null)
                return;
            
            SimpleLog.Debug($"Refresh called for job {job.Value.ToString()}");
            UpdateCurrentJobBar(reset ? DefaultConfig : Config, job.Value, preview);
        }
        
        private void UpdateCurrentJobBar(Configs config, uint job, bool preview) {
            // TODO: Preset dictionary
            SimpleLog.Debug("Redrawing");
            switch (job) {
                case 19:
                    UpdatePLD(config, preview);
                    break;
                case 20:
                    UpdateMNK(config, preview);
                    break;
                case 21:
                    UpdateWAR(config, preview);
                    break;
                case 22:
                    UpdateDRG(config, preview);
                    break;
                case 23:
                    UpdateBRD(config, preview);
                    break;
                // case 24:
                //     UpdateWHM(config, preview);
                //     break;
                // case 25:
                //     UpdateBLM(config, preview);
                //     break;
                // case 27:
                //     UpdateSMN(config, preview);
                //     break;
                // case 28:
                //     UpdateSCH(config, preview);
                //     break;
                // case 30:
                //     UpdateNIN(config, preview);
                //     break;
                case 31:
                    UpdateMCH(config, preview);
                    break;
                // case 32:
                //     UpdateDRK(config, preview);
                //     break;
                // case 33:
                //     UpdateAST(config, preview);
                //     break;
                // case 34:
                //     UpdateSAM(config, preview);
                //     break;
                case 35:
                    SimpleLog.Debug("Update RDM");
                    UpdateRDM(config, preview);
                    break;
                // case 37:
                //     UpdateGNB(config, preview);
                //     break;
                // case 38:
                //     UpdateDNC(config, preview);
                //     break;
                // case 39:
                //     UpdateRPR(config, preview);
                //     break;
                // case 40:
                //     UpdateSGE(config, preview);
                //     break;
            }
        }
        
        private void UpdateNode(AtkResNode* node, HideAndOffsetConfig config, bool preview) {
            if (node == null) {
                SimpleLog.Error("Node not found to update.");
                return;
            }
            
            if (config.Hide)
                node->Color.A = 0;
            else if (preview) {
                if ((node->Flags & 0x10) == 0)
                    node->Flags ^= 0x10; // TODO: Swap flag back after preview
                node->Color.A = 255;
                //
                // if (node->Type == NodeType.Text)
                // {
                //     var text = node->GetAsAtkTextNode();
                //     if (text->NodeText.IsEmpty > 0)
                //         text->NodeText.StringPtr = "15";
                // }
            }

            node->SetPositionFloat(config.OffsetX, config.OffsetY);
        }
        
        private void UpdatePLD(Configs config, bool preview) {
            var hudAddon = "JobHudPLD0";

            var baseAddon = Common.GetUnitBase(hudAddon);
            if (baseAddon == null)
                return;

            try {
                UpdateNode(baseAddon->GetNodeById(18), config.PLDOathBar, preview);
                UpdateNode(baseAddon->GetNodeById(17), config.PLDOathBarText, preview);
                UpdateNode(baseAddon->GetNodeById(15), config.PLDIronWillIndicator, preview);
                
                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
        
        private void UpdateMNK(Configs config, bool preview) {
            var hudAddon = "JobHudMNK0";
            var hudAddon2 = "JobHudMNK1";

            var masterAddon = Common.GetUnitBase(hudAddon);
            var chakraAddon = Common.GetUnitBase(hudAddon2);
            if (chakraAddon == null || masterAddon == null)
                return;

            try {
                UpdateNode(chakraAddon->GetNodeById(18), config.MNKChakra1, preview);
                UpdateNode(chakraAddon->GetNodeById(19), config.MNKChakra2, preview);
                UpdateNode(chakraAddon->GetNodeById(20), config.MNKChakra3, preview);
                UpdateNode(chakraAddon->GetNodeById(21), config.MNKChakra4, preview);
                UpdateNode(chakraAddon->GetNodeById(22), config.MNKChakra5, preview);
                UpdateNode(masterAddon->GetNodeById(38), config.MNKText, preview);
                UpdateNode(masterAddon->GetNodeById(34), config.MNKBeastChakra1, preview);
                UpdateNode(masterAddon->GetNodeById(35), config.MNKBeastChakra2, preview);
                UpdateNode(masterAddon->GetNodeById(36), config.MNKBeastChakra3, preview);
                UpdateNode(masterAddon->GetNodeById(26), config.MNKLunarNadi, preview);
                UpdateNode(masterAddon->GetNodeById(29), config.MNKSolarNadi, preview);

                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
        
        private void UpdateWAR(Configs config, bool preview) {
            var hudAddon = "JobHudWAR0";

            var baseAddon = Common.GetUnitBase(hudAddon);
            if (baseAddon == null)
                return;

            try {
                UpdateNode(baseAddon->GetNodeById(14), config.WARDefiance, preview);
                UpdateNode(baseAddon->GetNodeById(16), config.WARBarText, preview);
                UpdateNode(baseAddon->GetNodeById(17), config.WARBeastBar, preview);

                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
        
        private void UpdateDRG(Configs config, bool preview) {
            var hudAddon = "JobHudDRG0";

            var baseAddon = Common.GetUnitBase(hudAddon);
            if (baseAddon == null)
                return;

            try {
                UpdateNode(baseAddon->GetNodeById(42), config.DRGDragonGaugeText, preview);
                UpdateNode(baseAddon->GetNodeById(43), config.DRGDragonGauge, preview);
                UpdateNode(baseAddon->GetNodeById(36), config.DRGGaze1, preview);
                UpdateNode(baseAddon->GetNodeById(37), config.DRGGaze2, preview);
                UpdateNode(baseAddon->GetNodeById(39), config.DRGMind1, preview);
                UpdateNode(baseAddon->GetNodeById(40), config.DRGMind2, preview);

                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
        
        private void UpdateBRD(Configs config, bool preview) {
            var hudAddon = "JobHudBRD0";

            var baseAddon = Common.GetUnitBase(hudAddon);
            if (baseAddon == null)
                return;

            try {
                UpdateNode(baseAddon->GetNodeById(99), config.BRDSongBar, preview);
                UpdateNode(baseAddon->GetNodeById(76), config.BRDSongName, preview);
                UpdateNode(baseAddon->GetNodeById(90), config.BRDRepertoire1, preview);
                UpdateNode(baseAddon->GetNodeById(91), config.BRDRepertoire2, preview);
                UpdateNode(baseAddon->GetNodeById(92), config.BRDRepertoire3, preview);
                UpdateNode(baseAddon->GetNodeById(93), config.BRDRepertoire4, preview);
                UpdateNode(baseAddon->GetNodeById(94), config.BRDRepertoire1, preview);
                UpdateNode(baseAddon->GetNodeById(95), config.BRDRepertoire2, preview);
                UpdateNode(baseAddon->GetNodeById(96), config.BRDRepertoire3, preview);
                UpdateNode(baseAddon->GetNodeById(97), config.BRDSongCountdown, preview);
                UpdateNode(baseAddon->GetNodeById(86), config.BRDSoulVoiceText, preview);
                UpdateNode(baseAddon->GetNodeById(87), config.BRDSoulVoiceBar, preview);
                UpdateNode(baseAddon->GetNodeById(79), config.BRDMageCoda, preview);
                UpdateNode(baseAddon->GetNodeById(82), config.BRDMageCoda, preview);
                UpdateNode(baseAddon->GetNodeById(80), config.BRDArmyCoda, preview);
                UpdateNode(baseAddon->GetNodeById(84), config.BRDArmyCoda, preview);
                UpdateNode(baseAddon->GetNodeById(71), config.BRDWandererCoda, preview);
                UpdateNode(baseAddon->GetNodeById(83), config.BRDWandererCoda, preview);

                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
        
        private void UpdateMCH(Configs config, bool preview) {
            var hudAddon = "JobHudMCH0";

            var baseAddon = Common.GetUnitBase(hudAddon);
            if (baseAddon == null)
                return;

            try {
                UpdateNode(baseAddon->GetNodeById(38), config.MCHHeatBar, preview);
                UpdateNode(baseAddon->GetNodeById(37), config.MCHHeatText, preview);
                UpdateNode(baseAddon->GetNodeById(36), config.MCHOverheatIcon, preview);
                UpdateNode(baseAddon->GetNodeById(35), config.MCHOverheatText, preview);
                
                UpdateNode(baseAddon->GetNodeById(43), config.MCHBatteryBar, preview);
                UpdateNode(baseAddon->GetNodeById(42), config.MCHBatteryText, preview);
                UpdateNode(baseAddon->GetNodeById(41), config.MCHQueenIcon, preview);
                UpdateNode(baseAddon->GetNodeById(40), config.MCHQueenText, preview);
                
                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
        private void UpdateRDM(Configs config, bool preview) {
            var hudAddon = $"JobHudRDM0";

            var baseAddon = Common.GetUnitBase(hudAddon);
            if (baseAddon == null)
                return;
            
            SimpleLog.Log(config.RDMBlackManaBar.Hide.ToString());

            try {
                UpdateNode(baseAddon->GetNodeById(38), config.RDMWhiteManaBar, preview);
                UpdateNode(baseAddon->GetNodeById(39), config.RDMBlackManaBar, preview);
                UpdateNode(baseAddon->GetNodeById(35), config.RDMStatusIndicator, preview);
                UpdateNode(baseAddon->GetNodeById(27), config.RDMManaStacks, preview);
                UpdateNode(baseAddon->GetNodeById(26), config.RDMBlackManaText, preview);
                UpdateNode(baseAddon->GetNodeById(25), config.RDMWhiteManaText, preview);
                
                // TODO: User selectable colors for each? Better UX for editor (put inline)
            } catch (Exception ex) {
                SimpleLog.Error(ex.Message);
            }
        }
    }
}