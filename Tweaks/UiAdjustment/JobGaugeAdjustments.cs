using System;
using System.Collections.Generic;
using System.Linq;
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
        public override string Name => "Job Gauge Adjustments";
        public override string Description => "Allows moving and hiding parts of simple job gauges";
        protected override string Author => "Tinuviel";
        public override IEnumerable<string> Tags => new[] { "job", "gauge" };
        
        private Configs Config { get; set; }
        private readonly Dictionary<(GaugeComponentConfig, uint), GaugeComponentDefault> defaultValues = new();
        private uint? lastJob;
        private int? updateCount;

        #region Helper Classes

        private class JobPieceMap {
            internal JobPieceMap(string key, string configName, params uint[] nodeIds) {
                Key = key;
                ConfigName = configName;
                NodeIds = nodeIds;
            }

            internal string Key { get; }
            internal string ConfigName { get; }
            internal uint[] NodeIds { get; }
        }

        private class JobInfo {
            internal JobInfo(uint jobId, string jobName) {
                JobId = jobId;
                JobName = jobName;
            }
            internal string JobName { get; }
            internal uint JobId { get; }
        }
        
        #endregion

        #region Per-Job Mappings
        
        private readonly Dictionary<JobInfo, Dictionary<string, JobPieceMap[]>> JobMap =
            new() {
                {
                    new JobInfo(19, "Paladin"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudPLD0",
                            new[] {
                                new JobPieceMap("Oath", "Oath Gauge", 18),
                                new JobPieceMap("OathText", "Oath Gauge Value", 17),
                                new JobPieceMap("IronWill", "Iron Will Icon", 15)
                            }
                        }
                    }
                },
                {
                    new JobInfo(20, "Monk"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudMNK1",
                            new[] {
                                new JobPieceMap("Chakra1", "Chakra 1", 18),
                                new JobPieceMap("Chakra2", "Chakra 2", 19),
                                new JobPieceMap("Chakra3", "Chakra 3", 20),
                                new JobPieceMap("Chakra4", "Chakra 4", 21),
                                new JobPieceMap("Chakra5", "Chakra 5", 22)
                            }
                        },
                        {
                            "JobHudMNK0",
                            new[] {
                                new JobPieceMap("Master", "Master Timeout", 38),
                                new JobPieceMap("MChakra", "Master Chakras", 33),
                                new JobPieceMap("Nadi", "Nadi", 25),
                            }
                        }
                    }
                },
                {
                    new JobInfo(21, "Warrior"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudWAR0",
                            new[] {
                                new JobPieceMap("Defiance", "Defiance Icon", 14),
                                new JobPieceMap("Beast", "Beast Gauge", 17),
                                new JobPieceMap("BeastValue", "Beast Gauge Value", 16),
                            }
                        }
                    }
                },
                {
                    new JobInfo(22, "Dragoon"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudDRG0",
                            new[] {
                                new JobPieceMap("LotD", "LotD Bar", 43),
                                new JobPieceMap("LotDValue", "LotD Bar Value", 42),
                                new JobPieceMap("Gaze1", "Gaze 1", 36),
                                new JobPieceMap("Gaze2", "Gaze 2", 37),
                                new JobPieceMap("Firstmind1", "Firstmind 1", 39),
                                new JobPieceMap("Firstmind2", "Firstmind 2", 40),
                            }
                        }
                    }
                },
                {
                    new JobInfo(23, "Bard"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudBRD0",
                            new[] {
                                new JobPieceMap("SongGauge", "Song Gauge", 99),
                                new JobPieceMap("SongGaugeValue", "Song Gauge Value", 97),
                                new JobPieceMap("SongName", "Song Name", 76),
                                new JobPieceMap("Repertoire1","Repertoire 1", 90, 94),
                                new JobPieceMap("Repertoire2","Repertoire 2", 91, 95),
                                new JobPieceMap("Repertoire3","Repertoire 3", 92, 96),
                                new JobPieceMap("Repertoire4","Repertoire 4", 93),
                                new JobPieceMap("SoulVoiceBar","Soul Voice Bar", 87),
                                new JobPieceMap("SoulVoiceText","Soul Voice Text", 86),
                                new JobPieceMap("MageCoda","Mage's Coda", 79, 82),
                                new JobPieceMap("ArmyCoda","Army's Coda", 80, 84),
                                new JobPieceMap("WandererCoda","Wanderer's Coda", 71, 83)
                            }
                        }
                    }
                },
                {
                    new JobInfo(24, "White Mage"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudWHM0",
                            new[] {
                                new JobPieceMap("LilyBar","Lily Bar", 38),
                                new JobPieceMap("Lily1","Lily 1", 30),
                                new JobPieceMap("Lily2","Lily 2", 31),
                                new JobPieceMap("Lily3","Lily 3", 32),
                                new JobPieceMap("BloodLily1","Blood Lily 1", 34),
                                new JobPieceMap("BloodLily2","Blood Lily 2", 35),
                                new JobPieceMap("BloodLily3","Blood Lily 3", 36),
                            }
                        }
                    }
                },
                {
                    new JobInfo(25, "Black Mage"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudBLM0",
                            new[] {
                                new JobPieceMap("CountdownText","Countdown Text", 36),
                                new JobPieceMap("Ele1","Ice/Fire 1", 42),
                                new JobPieceMap("Ele2","Ice/Fire 2", 43),
                                new JobPieceMap("Ele3","Ice/Fire 3", 44),
                                new JobPieceMap("Heart1","Heart 1", 38),
                                new JobPieceMap("Heart2","Heart 2", 39),
                                new JobPieceMap("Heart3","Heart 3", 40),
                                new JobPieceMap("PolygotGauge","Polygot Gauge", 48),
                                new JobPieceMap("Polygot1","Polygot 1", 46),
                                new JobPieceMap("Polygot2","Polygot 2", 47),
                                new JobPieceMap("Endochan","Endochan", 33),
                                new JobPieceMap("ParadoxGauge","Paradox Gauge", 34)
                            }
                        }
                    }
                },
                {
                    new JobInfo(27, "Summoner"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudSMN1",
                            new[] {
                                new JobPieceMap("TranceGauge","Trance Gauge", 56),
                                new JobPieceMap("TranceCountdown","Trance Countdown", 55),
                                new JobPieceMap("RubyArcanum","Ruby Arcanum", 51),
                                new JobPieceMap("TopazArcanum","Topaz Arcanum", 52),
                                new JobPieceMap("EmeraldArcanum","Emerald Arcanum", 53),
                                new JobPieceMap("PetCountdown","Pet Countdown", 50),
                                new JobPieceMap("PetIcon","Pet Icon", 49),
                                new JobPieceMap("BahamutPhoenix","Bahamut/Phoenix", 47),
                            }
                        },
                        {
                            "JobHudSMN0",
                            new[] {
                                new JobPieceMap("Aetherflow1","Aetherflow 1", 12),
                                new JobPieceMap("Aetherflow2","Aetherflow 2", 13),
                            }
                        }
                    }
                },
                {
                    new JobInfo(28, "Scholar"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudSCH0",
                            new[] {
                                new JobPieceMap("FaireGauge","Faire Gauge", 32),
                                new JobPieceMap("FaireGaugeValue","Faire Gauge Value", 31),
                                new JobPieceMap("SeraphIcon","Seraph Icon", 29),
                                new JobPieceMap("SeraphCountdown","Seraph Countdown", 30),
                            }
                        },
                        {
                            "JobHudACN0",
                            new[] {
                                new JobPieceMap("Aetherflow1","Aetherflow 1", 8),
                                new JobPieceMap("Aetherflow2","Aetherflow 2", 9),
                                new JobPieceMap("Aetherflow3","Aetherflow 3", 10),
                            }
                        }
                    }
                },
                {
                    new JobInfo(30, "Ninja"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudNIN1",
                            new[] {
                                new JobPieceMap("HutonBar","Huton Bar", 20),
                                new JobPieceMap("HutonBarValue","Huton Bar Value", 19),
                                new JobPieceMap("HutonClockIcon","Huton Clock Icon", 18)
                            }
                        },
                        {
                            "JobHudNIN0",
                            new[] {
                                new JobPieceMap("NinkiGauge","Ninki Gauge", 19),
                                new JobPieceMap("NinkiGaugeValue","Ninki Gauge Value", 18)
                            }
                        }
                    }
                },
                {
                    new JobInfo(31, "Mechanist"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudMCH0",
                            new[] {
                                new JobPieceMap("HeatGauge","Heat Gauge", 38),
                                new JobPieceMap("HeatValue","Heat Gauge Value", 37),
                                new JobPieceMap("OverheatIcon","Overheat Icon", 36),
                                new JobPieceMap("OverheatCountdown","Overheat Countdown", 35),
                                new JobPieceMap("BatteryGauge","Battery Gauge", 43),
                                new JobPieceMap("BatteryValue","Battery Value", 42),
                                new JobPieceMap("QueenIcon","Queen Icon", 41),
                                new JobPieceMap("QueenCountdown","QueenCountdown", 40),
                            }
                        }
                    }
                },
                {
                    new JobInfo(32, "Dark Knight"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudDRK0",
                            new[] {
                                new JobPieceMap("GritIcon","Grit Icon", 15),
                                new JobPieceMap("BloodGauge","Blood Gauge", 18),
                                new JobPieceMap("BloodGaugeValue","Blood Gauge Value", 17)
                            }
                        },
                        {
                            "JobHudDRK1",
                            new[] {
                                new JobPieceMap("DarksideGauge", "Darkside Gauge", 27),
                                new JobPieceMap("DarksideGaugeValue", "Darkside Gauge Value", 26),
                                new JobPieceMap("DarkArts","Dark Arts", 24),
                                new JobPieceMap("LivingShadow","Living Shadow", 22),
                                new JobPieceMap("LivingShadowValue","Living Shadow Value", 23),
                            }
                        }
                    }
                },
                {
                    new JobInfo(33, "Astrologian"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudAST0",
                            new[] {
                                new JobPieceMap("Background", "Background", 42, 43),
                                new JobPieceMap("Arcanum", "Arcanum", 38),
                                new JobPieceMap("DrawBackground", "Arcanum Background", 40),
                                new JobPieceMap("AstrosignBkg", "Astrosign Background", 36),
                                new JobPieceMap("Astrosigns", "Astrosigns", 33, 34, 35),
                                new JobPieceMap("MinorArcanum", "Minor Arcanum", 39),
                                new JobPieceMap("MinorBackground", "Minor Arcanum Background", 41),
                            }
                        }
                    }
                },
                {
                    new JobInfo(34, "Samurai"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudSAM1",
                            new[] {
                                new JobPieceMap("Sen1","Sen 1", 41),
                                new JobPieceMap("Sen2","Sen 2", 45),
                                new JobPieceMap("Sen3","Sen 3", 49),
                            }
                        },
                        {
                            "JobHudSAM0",
                            new[] {
                                new JobPieceMap("Kenki","Kenki Gauge", 31),
                                new JobPieceMap("KenkiValue","Kenki Value", 30),
                                new JobPieceMap("Meditation1","Meditation 1", 26),
                                new JobPieceMap("Meditation2","Meditation 2", 27),
                                new JobPieceMap("Meditation3","Meditation 3", 28),
                            }
                        }
                    }
                },
                {
                    new JobInfo(35, "Red Mage"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudRDM0",
                            new[] {
                                new JobPieceMap("WhiteManaBar","White Mana Bar", 38),
                                new JobPieceMap("WhiteManaValue","White Mana Value", 25),
                                new JobPieceMap("BlackManaBar","Black Mana Bar", 39),
                                new JobPieceMap("BlackManaValue","Black Mana Value", 26),
                                new JobPieceMap("StatusIndicator","Status Indicator", 35),
                                new JobPieceMap("ManaStacks","Mana Stacks", 27), // TODO: Individual Stacks
                            }
                        }
                    }
                },
                {
                    new JobInfo(37, "Gunbreaker"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudGNB0",
                            new[] {
                                new JobPieceMap("RoyalGuard","Royal Guard Icon", 24),
                                new JobPieceMap("Cart1","Cartridge 1", 20),
                                new JobPieceMap("Cart2","Cartridge 2", 22),
                                new JobPieceMap("Cart3","Cartridge 3", 23),
                            }
                        }
                    }
                },
                // TODO: Dancer is a pain in the ass and will require updates on element visibility change
                {
                    new JobInfo(38, "Dancer"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudDNC0",
                            new[] {
                                new JobPieceMap("Standard","Standard Step Icon", 13),
                            }
                        }
                    }
                },
                {
                    new JobInfo(39, "Reaper"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudRRP1",
                            new[] {
                                new JobPieceMap("Shroud1","Lemure Shroud 1", 21),
                                new JobPieceMap("Shroud2","Lemure Shroud 2", 20),
                                new JobPieceMap("Shroud3","Lemure Shroud 3", 19),
                                new JobPieceMap("Shroud4","Lemure Shroud 4", 18),
                                new JobPieceMap("Shroud5","Lemure Shroud 5", 17),
                                new JobPieceMap("Enshroud","Enshroud Icon", 15),
                                new JobPieceMap("EnshroudIcon","Enshroud Countdown", 16),
                            }
                        },
                        {
                            "JobHudRRP0",
                            new[] {
                                new JobPieceMap("ShroudGauge","Shroud Gauge", 45),
                                new JobPieceMap("ShroudValue","Shroud Gauge Value", 44),
                                new JobPieceMap("DeathGauge","Death Gauge", 42),
                                new JobPieceMap("DeathValue","Death Gauge Value", 41),
                            }
                        }
                    }
                },
                {
                    new JobInfo(40, "Sage"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudGFF1",
                            new[] {
                                new JobPieceMap("AddersgallGauge","Addersgall Gauge", 34),
                                new JobPieceMap("Addersgall1","Addersgall 1", 27),
                                new JobPieceMap("Addersgall2","Addersgall 2", 28),
                                new JobPieceMap("Addersgall3","Addersgall 3", 29),
                                new JobPieceMap("Addersting1","Addersting 1", 31),
                                new JobPieceMap("Addersting2","Addersting 2", 32),
                                new JobPieceMap("Addersting3","Addersting 3", 33),
                            }
                        },
                        {
                            "JobHudGFF0",
                            new[] {
                                new JobPieceMap("Eukrasia","Eukrasia Icon", 17),
                            }
                        }
                    }
                }
            };
        
        #endregion

        #region Configuration

        public class GaugeComponentConfig {
            public bool Hide;
            public int OffsetX;
            public int OffsetY;
        }

        public class GaugeComponentDefault {
            public int X;
            public int Y;
            public byte A;
        }
        
        public class JobConfig {
            public bool Enabled;
            public Dictionary<string, GaugeComponentConfig> Components = new();
        }

        public class Configs : TweakConfig {
            public Dictionary<uint, JobConfig> Jobs = new();
        }

        #endregion

        public override void Enable() {
            try {
                Config = LoadConfig<Configs>() ?? new Configs();
            } catch (Exception ex) {
                Config = new Configs(); // TODO: Remove after first setup
            }

            Service.Framework.Update += OnFrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            UpdateCurrentJobBar(true);
            Service.Framework.Update -= OnFrameworkUpdate;
            lastJob = null;
            
            base.Disable();
        }

        private bool PieceEditor(string label, GaugeComponentConfig config) {
            var hasChanged = false;
            
            ImGui.Text(label);
            ImGui.SameLine();
            
            ImGui.SetCursorPosX(300 * ImGuiHelpers.GlobalScale);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##resetOffsetX_{label}")) {
                config.OffsetX = 0;
                config.OffsetY = 0;
                config.Hide = false;
                hasChanged = true;
            }
            ImGui.PopFont();
            
            hasChanged |= ImGui.Checkbox(LocString("Hide"), ref config.Hide);
            if (config.Hide) 
                return hasChanged;
            
            ImGui.Text(LocString("X Offset: "));
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            hasChanged |= ImGui.InputInt($"##offsetX_{label}", ref config.OffsetX);
            
            ImGui.Text(LocString("Y Offset: "));
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            hasChanged |= ImGui.InputInt($"##offsetY_{label}", ref config.OffsetY);
            return hasChanged;
        }

        private uint configSelectedJob;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.BeginChild("JobStack::JobGaugeAdjustments", new Vector2(ImGui.GetContentRegionAvail().X, 435 * ImGuiHelpers.GlobalScale), true);
            ImGui.Columns(2);
            ImGui.SetColumnWidth(0, 150 * ImGuiHelpers.GlobalScale);

            ImGui.BeginChild("JobStack::JobGaugeAdjustments::JobTree");

            foreach (var jobInfo in JobMap.Keys) {
                if (ImGui.Selectable(jobInfo.JobName, configSelectedJob == jobInfo.JobId))
                    configSelectedJob = jobInfo.JobId;
            }

            ImGui.EndChild();
            ImGui.NextColumn();

            var selectedJob = JobMap.Keys.FirstOrDefault(m => m.JobId == configSelectedJob);
            if (selectedJob != null) {
                hasChanged |= DrawConfigJobSection(selectedJob);
            }

            ImGui.Columns(1);
            ImGui.EndChild();

            if (!hasChanged) return;
            
            UpdateCurrentJobBar(false);
            SaveConfig(Config);
        };
        
        private bool DrawConfigJobSection(JobInfo jobInfo) {
            var hasChanged = false;
            if (!Config.Jobs.TryGetValue(jobInfo.JobId, out var jobConfig)) {
                jobConfig = new JobConfig();
                Config.Jobs[jobInfo.JobId] = jobConfig;
            }
            
            // Draw name + Enable flag, expand as needed
            hasChanged |= ImGui.Checkbox(LocString("Enable"), ref jobConfig.Enabled);
            if (jobConfig.Enabled) {
                var pieces = JobMap[jobInfo].Values.SelectMany(v => v).ToArray();
                for (var i = 0; i < pieces.Length; i++) {
                    if (!jobConfig.Components.TryGetValue(pieces[i].Key, out var componentConfig)) {
                        componentConfig = new GaugeComponentConfig();
                        jobConfig.Components[pieces[i].Key] = componentConfig;
                    }
                    
                    hasChanged |= PieceEditor(LocString(pieces[i].ConfigName), componentConfig);
                    if (i < pieces.Length - 1)
                        ImGui.Separator();
                }
            }

            return hasChanged;
        }
        
        private void OnFrameworkUpdate(Framework framework) {
            // TODO: Detect offset shift made by game and update defaults accordingly so the shifts are accounted for
            
            try {
                // TODO: Fix AST being a complete pain in the ass
                var job = Service.ClientState.LocalPlayer?.ClassJob.Id;
                if (job == null)
                    return;

                if (updateCount.HasValue)
                    updateCount++;

                // After a job changes, the gauges do not update instantly. We must wait a bit. A few framework ticks should be good
                if (lastJob == job || updateCount is < 5)
                    return;

                
                if (updateCount.HasValue) {
                    SimpleLog.Debug("Frame wait complete, updating");
                    UpdateCurrentJobBar(false, job);
                    lastJob = job;
                    updateCount = null;
                } else {
                    SimpleLog.Debug("Job change detected, waiting frames");
                    updateCount = 0;
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void UpdateCurrentJobBar(bool reset, uint? job = null) {
            job ??= Service.ClientState.LocalPlayer?.ClassJob.Id;
            if (job == null)
                return;
            
            SimpleLog.Debug($"Refresh called for job {job.Value.ToString()}");
            UpdateCurrentJob(job.Value, reset);
        }

        private void UpdateNode(AtkResNode* node, GaugeComponentConfig config, bool reset) {
            if (node == null) {
                SimpleLog.Error("Node not found to update.");
                return;
            }

            // TODO: TryGetValue
            if (!defaultValues.ContainsKey((config, node->NodeID))) {
                defaultValues[(config, node->NodeID)] = new GaugeComponentDefault {
                    X = (int) node->X,
                    Y = (int) node->Y,
                    A = node->Color.A
                };
            }
            var defaults = defaultValues[(config, node->NodeID)];

            if (reset) {
                node->SetPositionFloat(defaults.X, defaults.Y);
                return;
            }

            if (config.Hide)
                node->Color.A = 0;
            else
                node->Color.A = defaults.A;
            
            node->SetPositionFloat(defaults.X + config.OffsetX, defaults.Y + config.OffsetY);
        }

        private void UpdateCurrentJob(uint job, bool forceReset) {
            if (!Config.Jobs.TryGetValue(job, out var jobConfig)) {
                jobConfig = new JobConfig();
                Config.Jobs[job] = jobConfig;
            }

            var reset = forceReset | !jobConfig.Enabled;
            var components = jobConfig.Components;
            
            var info = JobMap.Keys.FirstOrDefault(map => map.JobId == job);
            if (info == null) {
                SimpleLog.Debug($"Job with Id {job} not found in map. Cannot update.");
                return;
            }
            
            var maps = JobMap[info];
            foreach (var addonMap in maps) {
                var hudAddon = Common.GetUnitBase(addonMap.Key);
                if (hudAddon == null) {
                    SimpleLog.Debug($"Could not get base addon {addonMap.Key} for job {job} in render.");
                    return;
                }

                foreach (var piece in addonMap.Value) {
                    foreach (var nodeId in piece.NodeIds) {
                        if (!jobConfig.Components.TryGetValue(piece.Key, out var componentConfig)) {
                            componentConfig = new GaugeComponentConfig();
                            jobConfig.Components[piece.Key] = componentConfig;
                        }
                        
                        UpdateNode(hudAddon->GetNodeById(nodeId), components[piece.Key], reset);
                    }
                }
            }
        }
    }
}