using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Hooking;
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
        public override IEnumerable<string> Tags => new[] { "job", "gauge", "ui" };

        private Configs config;
        private uint configSelectedJob;
        
        private readonly Dictionary<string, Dictionary<uint, GaugeComponentTracking>> trackedNodeValues = new();
        private readonly Dictionary<string, List<IntPtr>> trackedNodes = new();
        private int? jobChangeFrameCount;
        private uint? lastJobSet;
        
        private delegate byte AddonOnUpdate(AtkUnitBase* atkUnitBase);
        private readonly Dictionary<string, Hook<AddonOnUpdate>> addonUpdateHooks = new();
        
        public override void Enable() {
            config = LoadConfig<Configs>() ?? new Configs();
            DetachJob();

            // Note: Update loop will trigger initial job selection
            Service.Framework.Update += OnFrameworkUpdate;
            base.Enable();
        }
        
        public override void Disable() {
            UpdateCurrentJobBar(true);
            DetachJob();
            
            Service.Framework.Update -= OnFrameworkUpdate;
            lastJobSet = null;
            
            base.Disable();
        }
        
        private void DetachJob() {
            SimpleLog.Debug("Resetting Tracked Info");
            
            trackedNodes.Clear();
            trackedNodeValues.Clear();

            foreach (var hook in addonUpdateHooks.Values) {
                hook?.Disable();
            }
            addonUpdateHooks.Clear();
        }

        private void OnFrameworkUpdate(Framework framework) {
            try {
                var job = Service.ClientState.LocalPlayer?.ClassJob.Id;
                if (job == null)
                    return;

                if (jobChangeFrameCount.HasValue)
                    jobChangeFrameCount++;

                // After a job changes, the gauges do not update instantly. We must wait a bit.
                // 1 tick is enough for them to show up, but need a few more for correct element visibility
                if (lastJobSet == job || jobChangeFrameCount is < 5)
                    return;

                if (jobChangeFrameCount.HasValue) {
                    SimpleLog.Debug("Waiting for job change complete. Beginning Initialization.");
                    InitializeJob(job.Value);
                    UpdateCurrentJobBar(false, job);
                    jobChangeFrameCount = null;
                } else {
                    SimpleLog.Debug("Job change detected, waiting frames");
                    DetachJob();
                    jobChangeFrameCount = 0;
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
        
        private void InitializeJob(uint job) {
            SimpleLog.Debug("Initializing Job");
            trackedNodes.Clear();
            trackedNodeValues.Clear();
            lastJobSet = job;
            
            UpdateCurrentJobBar(false, job);
        }
        
        private void UpdateTrackedNodes(string addonName) {
            if (trackedNodes == null)
                return;

            if (!trackedNodes.ContainsKey(addonName))
                return;

            foreach (var node in trackedNodes[addonName]) {
                UpdateTrackedNode(addonName, (AtkResNode*) node);
            }
        }

        private void UpdateCurrentJobBar(bool reset, uint? job = null) {
            job ??= Service.ClientState.LocalPlayer?.ClassJob.Id;
            if (job == null)
                return;
            
            SimpleLog.Debug($"Refresh called for job {job.Value.ToString()}");
            UpdateJobGauges(job.Value, reset);
        }
        
        private GaugeComponentTracking GetTrackedNode(string addonName, AtkResNode* node) {
            if (node == null)
                return null;

            if (!trackedNodeValues.TryGetValue(addonName, out var addonTrackings)) {
                addonTrackings = new Dictionary<uint, GaugeComponentTracking>();
                trackedNodeValues[addonName] = addonTrackings;
            }

            addonTrackings.TryGetValue(node->NodeID, out var tracking);
            if (tracking != null) 
                return tracking;
            
            tracking = new GaugeComponentTracking {
                DefaultX = (int) node->X,
                DefaultY = (int) node->Y
            };
            
            addonTrackings[node->NodeID] = tracking;
            return tracking;
        }

        private void UpdateTrackedNode(string addonName, AtkResNode* node) {
            if (node == null)
                return;
            
            var tracking = GetTrackedNode(addonName, node);

            var xDiff = (int) node->X - tracking.LastX;
            var yDiff = (int) node->Y - tracking.LastY;

            if (xDiff != 0) {
                tracking.AdditionalX += xDiff;
                SimpleLog.Debug($"X Shifted for Node ID {node->NodeID} by {xDiff}. New offset {tracking.AdditionalX}");
                
                tracking.LastX = (int) node->X;
                SimpleLog.Debug($"New LastX for Node ID {node->NodeID}: {tracking.LastX}");
            }
            if (yDiff != 0) {
                tracking.AdditionalY += yDiff;
                SimpleLog.Debug($"Y Shifted for Node ID {node->NodeID} by {yDiff}. New offset {tracking.AdditionalY}");
                
                tracking.LastY = (int) node->Y;
                SimpleLog.Debug($"New LastY for Node ID {node->NodeID}: {tracking.LastY}");
            }

            var map = GetJobMap(lastJobSet);
            if (map == null)
                return;

            var jobConfig = GetJobConfig(lastJobSet!.Value);
            var addonPart = map.Addons[addonName].FirstOrDefault(p => p.NodeIds.Contains(node->NodeID));
            if (jobConfig.Components[addonPart!.Key].Hide && node->Color.A != 0)
                node->Color.A = 0;
        }
        
        private void UpdateNode(string addonName, AtkResNode* node, GaugeComponentConfig componentConfig, bool reset) {
            if (node == null) {
                SimpleLog.Error("Node not found to update.");
                return;
            }

            var tracking = GetTrackedNode(addonName, node);
            if (reset) {
                if (node->Color.A == 0)
                    node->Color.A = 255;
                
                node->SetPositionFloat(tracking.DefaultX + tracking.AdditionalX, tracking.DefaultY + tracking.AdditionalY);
                return;
            }

            if (componentConfig.Hide && node->Color.A != 0)
                node->Color.A = 0;
            else if (!componentConfig.Hide && node->Color.A == 0)
                node->Color.A = 255;
            
            var intendedX = tracking.DefaultX + tracking.AdditionalX + componentConfig.OffsetX;
            var intendedY = tracking.DefaultY + tracking.AdditionalY + componentConfig.OffsetY;

            node->SetPositionFloat(intendedX, intendedY);
            tracking.LastX = intendedX;
            tracking.LastY = intendedY;
        }

        private void UpdateJobGauges(uint job, bool forceReset) {
            var jobConfig = GetJobConfig(job);
            if (!jobConfig.Enabled && !forceReset) {
                SimpleLog.Debug("Job is not enabled, not updating gauges.");
                return;
            }
            
            SimpleLog.Debug("Job found. Proceeding to update gauges.");
            if (forceReset)
                SimpleLog.Debug("Resetting Gauge to default state.");
            
            var reset = forceReset || !jobConfig.Enabled;

            var map = GetJobMap(job);
            if (map == null) {
                SimpleLog.Debug($"Job with Id {job} not found in map. Cannot update.");
                return;
            }
            
            foreach (var addonMap in map.Addons) {
                var hudAddon = Common.GetUnitBase(addonMap.Key);
                if (hudAddon == null) {
                    SimpleLog.Debug($"Could not get base addon {addonMap.Key} for job {job} in render.");
                    return;
                }
                HookAddonUpdate(addonMap.Key, hudAddon);

                if (!trackedNodes.TryGetValue(addonMap.Key, out var addonNodes)) {
                    addonNodes = new List<IntPtr>();
                    trackedNodes[addonMap.Key] = addonNodes;
                }

                foreach (var part in addonMap.Value) {
                    var componentConfig = GetComponentConfig(jobConfig, part.Key);
                    
                    foreach (var nodeId in part.NodeIds) {
                        var node = hudAddon->GetNodeById(nodeId);
                        if (node == null)
                            continue;
                        
                        if (!addonNodes.Contains((IntPtr) node))
                            addonNodes.Add((IntPtr) node);
                        
                        UpdateNode(addonMap.Key, node, componentConfig, reset);
                    }
                }
            }
        }
        
        private void HookAddonUpdate(string addonName, AtkUnitBase* hudAddon) {
            if (hudAddon == null || addonUpdateHooks.ContainsKey(addonName))
                return;

            addonUpdateHooks[addonName] = new Hook<AddonOnUpdate>(new IntPtr(hudAddon->AtkEventListener.vfunc[39]), 
                atkunitbase => OnUpdate(addonName, atkunitbase));
            addonUpdateHooks[addonName]?.Enable();
        }

        private byte OnUpdate(string addonName, AtkUnitBase* atkunitbase) {
            var result = addonUpdateHooks[addonName].Original(atkunitbase);
            if (lastJobSet.HasValue && !jobChangeFrameCount.HasValue) {
                UpdateTrackedNodes(addonName);
            }
            
            return result;
        }
        
        private JobConfig GetJobConfig(uint jobId) {
            if (config.Jobs.TryGetValue(jobId, out var jobConfig)) 
                return jobConfig;
            
            jobConfig = new JobConfig();
            config.Jobs[jobId] = jobConfig;
            return jobConfig;
        }

        private GaugeComponentConfig GetComponentConfig(JobConfig jobConfig, string pieceKey) {
            if (jobConfig.Components.TryGetValue(pieceKey, out var componentConfig)) 
                return componentConfig;
            
            componentConfig = new GaugeComponentConfig();
            jobConfig.Components[pieceKey] = componentConfig;

            return componentConfig;
        }
        
        private JobGaugeUiMap GetJobMap(uint? jobId) {
            if (!jobId.HasValue)
                return null;
            
            jobMap.TryGetValue(jobId.Value, out var uiMap);
            return uiMap;
        }
        
        #region Configuration UI

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var padding = ImGui.GetStyle().CellPadding.X;

            var allHeight = 300 * ImGuiHelpers.GlobalScale;
            var jobSelectorWidth = 150 * ImGuiHelpers.GlobalScale;
            var configWidth = availableWidth - padding * 2 - jobSelectorWidth;
            
            // Job List (Left)
            ImGui.BeginChild("JobGaugeAdjustments::JobList", new Vector2(jobSelectorWidth, allHeight), false);

            foreach (var job in jobMap.Values.OrderBy(j => j.Name)) {
                if (ImGui.Selectable(job.Name, configSelectedJob == job.Id))
                    configSelectedJob = job.Id;
            }
                
            ImGui.EndChild();
            ImGui.SameLine();

            // Job Configuration (Right)
            var selectedJob = GetJobMap(configSelectedJob);
            var rightFlags = selectedJob == null ? ImGuiWindowFlags.None : ImGuiWindowFlags.MenuBar;
            ImGui.BeginChild("JobGaugeAdjustments::Configuration", new Vector2(configWidth, allHeight), false, rightFlags);
            if (ImGui.BeginMenuBar())
            {
                ImGui.TextWrapped(LocString(selectedJob!.Name));
                ImGui.EndMenuBar();
            }

            if (selectedJob != null) {
                var jobConfig = GetJobConfig(selectedJob.Id);
                var wasEnabled = jobConfig.Enabled;
                hasChanged |= DrawConfigJobSection(selectedJob, jobConfig);

                if (lastJobSet.HasValue && lastJobSet == selectedJob.Id) {
                    var forceReset = hasChanged && !jobConfig.Enabled && wasEnabled;
                    if (hasChanged)
                        UpdateJobGauges(selectedJob.Id, forceReset);
                    
                    if (forceReset)
                        DetachJob();
                }
            } else {
                ImGui.TextWrapped(LocString("Select a Job!"));
            }
                
            ImGui.EndChild();

            if (!hasChanged)
                return;
            
            SaveConfig(config);
        };

        private bool DrawConfigJobSection(JobGaugeUiMap job, JobConfig jobConfig) {
            var hasChanged = false;

            if (job.ComingSoon) {
                ImGui.Text(LocString("Coming Soon!"));
                return false;
            }
            
            // Draw name + Enable flag, expand as needed
            hasChanged |= ImGui.Checkbox(LocString("Enable"), ref jobConfig.Enabled);
            if (!jobConfig.Enabled) 
                return hasChanged;

            var components = job.Addons.SelectMany(a => a.Value).ToArray();
            for (var i = 0; i < components.Length; i++) {
                var componentConfig = GetComponentConfig(jobConfig, components[i].Key);
                hasChanged |= DrawEditorForPiece(LocString(components[i].ConfigName), componentConfig);
                
                if (i < components.Length - 1)
                    ImGui.Separator();
            }

            return hasChanged;
        }

        private bool DrawEditorForPiece(string label, GaugeComponentConfig gaugeConfig) {
            var hasChanged = false;
            
            ImGui.Text(label);
            ImGui.SameLine();
            
            ImGui.SetCursorPosX(320 * ImGuiHelpers.GlobalScale);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##resetOffsetX_{label}")) {
                gaugeConfig.OffsetX = 0;
                gaugeConfig.OffsetY = 0;
                gaugeConfig.Hide = false;
                hasChanged = true;
            }
            ImGui.PopFont();
            
            hasChanged |= ImGui.Checkbox(LocString("Hide") + $"###hide_{label}", ref gaugeConfig.Hide);
            if (gaugeConfig.Hide) 
                return hasChanged;
            
            ImGui.Text(LocString("X Offset: "));
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            hasChanged |= ImGui.InputInt($"##offsetX_{label}", ref gaugeConfig.OffsetX);
            
            ImGui.Text(LocString("Y Offset: "));
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            hasChanged |= ImGui.InputInt($"##offsetY_{label}", ref gaugeConfig.OffsetY);
            return hasChanged;
        }
        
        #endregion

        #region Helper Classes

        private class GaugeComponentTracking {
            public int DefaultX;
            public int DefaultY;

            public int LastX;
            public int LastY;

            public int AdditionalX;
            public int AdditionalY;
        }

        private class AddonComponentPart {
            internal AddonComponentPart(string key, string configName, params uint[] nodeIds) {
                Key = key;
                ConfigName = configName;
                NodeIds = nodeIds;
            }

            internal string Key { get; }
            internal string ConfigName { get; }
            internal uint[] NodeIds { get; }
        }

        private class JobGaugeUiMap {
            public JobGaugeUiMap(uint id, string name, bool comingSoon = false) {
                Id = id;
                Name = name;
                ComingSoon = comingSoon;
            }
            
            internal uint Id { get; }
            internal string Name { get; }
            internal bool ComingSoon { get; }

            internal Dictionary<string, AddonComponentPart[]> Addons { get; set; }
        }
        
        #endregion

        #region Per-Job Mappings

        private readonly Dictionary<uint, JobGaugeUiMap> jobMap = new() {
            {
                19,
                new JobGaugeUiMap(19, "Paladin") {
                    Addons = new() {
                        {
                            "JobHudPLD0",
                            new[] {
                                new AddonComponentPart("Oath", "Oath Gauge", 18),
                                new AddonComponentPart("OathText", "Oath Gauge Value", 17),
                                new AddonComponentPart("IronWill", "Iron Will Icon", 15)
                            }
                        }
                    }
                }
            }, {
                20,
                new JobGaugeUiMap(20, "Monk") {
                    Addons = new() {
                        {
                            "JobHudMNK1",
                            new[] {
                                new AddonComponentPart("Chakra1", "Chakra 1", 18),
                                new AddonComponentPart("Chakra2", "Chakra 2", 19),
                                new AddonComponentPart("Chakra3", "Chakra 3", 20),
                                new AddonComponentPart("Chakra4", "Chakra 4", 21),
                                new AddonComponentPart("Chakra5", "Chakra 5", 22)
                            }
                        }, {
                            "JobHudMNK0",
                            new[] {
                                new AddonComponentPart("Master", "Master Timeout", 39),
                                new AddonComponentPart("MChakra", "Master Chakras", 33),
                                new AddonComponentPart("Nadi", "Nadi", 25),
                            }
                        }
                    }
                }
            }, {
                21,
                new JobGaugeUiMap(21, "Warrior") {
                    Addons = new() {
                        {
                            "JobHudWAR0",
                            new[] {
                                new AddonComponentPart("Defiance", "Defiance Icon", 14),
                                new AddonComponentPart("Beast", "Beast Gauge", 17),
                                new AddonComponentPart("BeastValue", "Beast Gauge Value", 16),
                            }
                        }
                    }
                }
            }, {
                22,
                new JobGaugeUiMap(22, "Dragoon") {
                    Addons = new() {
                        {
                            "JobHudDRG0",
                            new[] {
                                new AddonComponentPart("LotD", "LotD Bar", 43),
                                new AddonComponentPart("LotDValue", "LotD Bar Value", 42),
                                new AddonComponentPart("Gaze1", "Gaze 1", 36),
                                new AddonComponentPart("Gaze2", "Gaze 2", 37),
                                new AddonComponentPart("Firstmind1", "Firstmind 1", 39),
                                new AddonComponentPart("Firstmind2", "Firstmind 2", 40),
                            }
                        }
                    }
                }
            }, {
                23,
                new JobGaugeUiMap(23, "Bard") {
                    Addons = new() {
                        {
                            "JobHudBRD0",
                            new[] {
                                new AddonComponentPart("SongGauge", "Song Gauge", 99),
                                new AddonComponentPart("SongGaugeValue", "Song Gauge Value", 97),
                                new AddonComponentPart("SongName", "Song Name", 76),
                                new AddonComponentPart("Repertoire1", "Repertoire 1", 90, 94),
                                new AddonComponentPart("Repertoire2", "Repertoire 2", 91, 95),
                                new AddonComponentPart("Repertoire3", "Repertoire 3", 92, 96),
                                new AddonComponentPart("Repertoire4", "Repertoire 4", 93),
                                new AddonComponentPart("SoulVoiceBar", "Soul Voice Bar", 87),
                                new AddonComponentPart("SoulVoiceText", "Soul Voice Text", 86),
                                new AddonComponentPart("MageCoda", "Mage's Coda", 79, 82),
                                new AddonComponentPart("ArmyCoda", "Army's Coda", 80, 84),
                                new AddonComponentPart("WandererCoda", "Wanderer's Coda", 71, 83)
                            }
                        }
                    }
                }
            }, {
                24,
                new JobGaugeUiMap(24, "White Mage") {
                    Addons = new() {
                        {
                            "JobHudWHM0",
                            new[] {
                                new AddonComponentPart("LilyBar", "Lily Bar", 38),
                                new AddonComponentPart("Lily1", "Lily 1", 30),
                                new AddonComponentPart("Lily2", "Lily 2", 31),
                                new AddonComponentPart("Lily3", "Lily 3", 32),
                                new AddonComponentPart("BloodLily1", "Blood Lily 1", 34),
                                new AddonComponentPart("BloodLily2", "Blood Lily 2", 35),
                                new AddonComponentPart("BloodLily3", "Blood Lily 3", 36),
                            }
                        }
                    }
                }
            }, {
                25,
                new JobGaugeUiMap(25, "Black Mage") {
                    Addons = new() {
                        {
                            "JobHudBLM0",
                            new[] {
                                new AddonComponentPart("CountdownText", "Countdown Text", 36),
                                new AddonComponentPart("Ele1", "Ice/Fire 1", 42),
                                new AddonComponentPart("Ele2", "Ice/Fire 2", 43),
                                new AddonComponentPart("Ele3", "Ice/Fire 3", 44),
                                new AddonComponentPart("Heart1", "Heart 1", 38),
                                new AddonComponentPart("Heart2", "Heart 2", 39),
                                new AddonComponentPart("Heart3", "Heart 3", 40),
                                new AddonComponentPart("PolygotGauge", "Polygot Gauge", 48),
                                new AddonComponentPart("Polygot1", "Polygot 1", 46),
                                new AddonComponentPart("Polygot2", "Polygot 2", 47),
                                new AddonComponentPart("Endochan", "Endochan", 33),
                                new AddonComponentPart("ParadoxGauge", "Paradox Gauge", 34)
                            }
                        }
                    }
                }
            }, {
                27,
                new JobGaugeUiMap(27, "Summoner") {
                    Addons = new() {
                        {
                            "JobHudSMN1",
                            new[] {
                                new AddonComponentPart("TranceGauge", "Trance Gauge", 56),
                                new AddonComponentPart("TranceCountdown", "Trance Countdown", 55),
                                new AddonComponentPart("RubyArcanum", "Ruby Arcanum", 51),
                                new AddonComponentPart("TopazArcanum", "Topaz Arcanum", 52),
                                new AddonComponentPart("EmeraldArcanum", "Emerald Arcanum", 53),
                                new AddonComponentPart("PetCountdown", "Pet Countdown", 50),
                                new AddonComponentPart("PetIcon", "Pet Icon", 49),
                                new AddonComponentPart("BahamutPhoenix", "Bahamut/Phoenix", 47),
                            }
                        }, {
                            "JobHudSMN0",
                            new[] {
                                new AddonComponentPart("Aetherflow1", "Aetherflow 1", 12),
                                new AddonComponentPart("Aetherflow2", "Aetherflow 2", 13),
                            }
                        }
                    }
                }
            }, {
                28,
                new JobGaugeUiMap(28, "Scholar") {
                    Addons = new() {
                        {
                            "JobHudSCH0",
                            new[] {
                                new AddonComponentPart("FaireGauge", "Faire Gauge", 32),
                                new AddonComponentPart("FaireGaugeValue", "Faire Gauge Value", 31),
                                new AddonComponentPart("SeraphIcon", "Seraph Icon", 29),
                                new AddonComponentPart("SeraphCountdown", "Seraph Countdown", 30),
                            }
                        }, {
                            "JobHudACN0",
                            new[] {
                                new AddonComponentPart("Aetherflow1", "Aetherflow 1", 8),
                                new AddonComponentPart("Aetherflow2", "Aetherflow 2", 9),
                                new AddonComponentPart("Aetherflow3", "Aetherflow 3", 10),
                            }
                        }
                    }
                }
            }, {
                30,
                new JobGaugeUiMap(30, "Ninja") {
                    Addons = new() {
                        {
                            "JobHudNIN1",
                            new[] {
                                new AddonComponentPart("HutonBar", "Huton Bar", 20),
                                new AddonComponentPart("HutonBarValue", "Huton Bar Value", 19),
                                new AddonComponentPart("HutonClockIcon", "Huton Clock Icon", 18)
                            }
                        }, {
                            "JobHudNIN0",
                            new[] {
                                new AddonComponentPart("NinkiGauge", "Ninki Gauge", 19),
                                new AddonComponentPart("NinkiGaugeValue", "Ninki Gauge Value", 18)
                            }
                        }
                    }
                }
            }, {
                31,
                new JobGaugeUiMap(31, "Mechanist") {
                    Addons = new() {
                        {
                            "JobHudMCH0",
                            new[] {
                                new AddonComponentPart("HeatGauge", "Heat Gauge", 38),
                                new AddonComponentPart("HeatValue", "Heat Gauge Value", 37),
                                new AddonComponentPart("OverheatIcon", "Overheat Icon", 36),
                                new AddonComponentPart("OverheatCountdown", "Overheat Countdown", 35),
                                new AddonComponentPart("BatteryGauge", "Battery Gauge", 43),
                                new AddonComponentPart("BatteryValue", "Battery Value", 42),
                                new AddonComponentPart("QueenIcon", "Queen Icon", 41),
                                new AddonComponentPart("QueenCountdown", "QueenCountdown", 40),
                            }
                        }
                    }
                }
            }, {
                32,
                new JobGaugeUiMap(32, "Dark Knight") {
                    Addons = new() {
                        {
                            "JobHudDRK0",
                            new[] {
                                new AddonComponentPart("GritIcon", "Grit Icon", 15),
                                new AddonComponentPart("BloodGauge", "Blood Gauge", 18),
                                new AddonComponentPart("BloodGaugeValue", "Blood Gauge Value", 17)
                            }
                        }, {
                            "JobHudDRK1",
                            new[] {
                                new AddonComponentPart("DarksideGauge", "Darkside Gauge", 27),
                                new AddonComponentPart("DarksideGaugeValue", "Darkside Gauge Value", 26),
                                new AddonComponentPart("DarkArts", "Dark Arts", 24),
                                new AddonComponentPart("LivingShadow", "Living Shadow", 22),
                                new AddonComponentPart("LivingShadowValue", "Living Shadow Value", 23)
                            }
                        }
                    }
                }
            }, {
                33,
                new JobGaugeUiMap(33, "Astrologian") {
                    Addons = new() {
                        {
                            "JobHudAST0",
                            new[] {
                                new AddonComponentPart("Background", "Background", 42, 43),
                                new AddonComponentPart("Arcanum", "Arcanum", 38),
                                new AddonComponentPart("DrawBackground", "Arcanum Background", 40),
                                new AddonComponentPart("AstrosignBkg", "Astrosign Background", 36),
                                new AddonComponentPart("Astrosigns", "Astrosigns", 33, 34, 35),
                                new AddonComponentPart("MinorArcanum", "Minor Arcanum", 39),
                                new AddonComponentPart("MinorBackground", "Minor Arcanum Background", 41),
                            }
                        }
                    }
                }
            }, {
                34,
                new JobGaugeUiMap(34, "Samurai") {
                    Addons = new() {
                        {
                            "JobHudSAM1",
                            new[] {
                                new AddonComponentPart("Sen1", "Sen 1", 41),
                                new AddonComponentPart("Sen2", "Sen 2", 45),
                                new AddonComponentPart("Sen3", "Sen 3", 49),
                            }
                        }, {
                            "JobHudSAM0",
                            new[] {
                                new AddonComponentPart("Kenki", "Kenki Gauge", 31),
                                new AddonComponentPart("KenkiValue", "Kenki Value", 30),
                                new AddonComponentPart("Meditation1", "Meditation 1", 26),
                                new AddonComponentPart("Meditation2", "Meditation 2", 27),
                                new AddonComponentPart("Meditation3", "Meditation 3", 28),
                            }
                        }
                    }
                }
            }, {
                35,
                new JobGaugeUiMap(35, "Red Mage") {
                    Addons = new() {
                        {
                            "JobHudRDM0",
                            new[] {
                                new AddonComponentPart("WhiteManaBar", "White Mana Bar", 38),
                                new AddonComponentPart("WhiteManaValue", "White Mana Value", 25),
                                new AddonComponentPart("BlackManaBar", "Black Mana Bar", 39),
                                new AddonComponentPart("BlackManaValue", "Black Mana Value", 26),
                                new AddonComponentPart("StatusIndicator", "Status Indicator", 35),
                                new AddonComponentPart("ManaStack1", "Mana Stack 1", 28),
                                new AddonComponentPart("ManaStack2", "Mana Stack 2", 29),
                                new AddonComponentPart("ManaStack3", "Mana Stack 3", 30)
                            }
                        }
                    }
                }
            }, {
                37,
                new JobGaugeUiMap(37, "Gunbreaker") {
                    Addons = new() {
                        {
                            "JobHudGNB0",
                            new[] {
                                new AddonComponentPart("RoyalGuard", "Royal Guard Icon", 24),
                                new AddonComponentPart("Cart1", "Cartridge 1", 20),
                                new AddonComponentPart("Cart2", "Cartridge 2", 22),
                                new AddonComponentPart("Cart3", "Cartridge 3", 23),
                            }
                        }
                    }
                }
            }, {
                38,
                new JobGaugeUiMap(38, "Dancer", true)
                //     {
                //         "JobHudDNC0",
                //         new[] {
                //             new AddonComponentPart("Waiting","Waiting Icon", 13),
                //             new AddonComponentPart("Standard","Standard Step Background", 14),
                //             new AddonComponentPart("StandardGlow","Step Diamonds", 12),
                //             new AddonComponentPart("StandardIcon","Standard Step Icon", 8),
                //             new AddonComponentPart("StandardText","Standard Step Countdown", 10, 11),
                //             new AddonComponentPart("Step1","Step 1", 3),
                //             new AddonComponentPart("Step2","Step 2", 4),
                //             new AddonComponentPart("Step3","Step 3", 5),
                //             new AddonComponentPart("Step4","Step 4", 38),
                //             new AddonComponentPart("StepHighlight","Current Step Highlight", 4),
                //             
                //             new AddonComponentPart("TechnicalBkg","Technical Step Background", 15),
                //         }
                //     },
                //     {
                //         "JobHudDNC1",
                //         new[] {
                //             new AddonComponentPart("Feather1","Fourfold Feather 1", 4),
                //             new AddonComponentPart("Feather2","Fourfold Feather 2", 5),
                //             new AddonComponentPart("Feather3","Fourfold Feather 3", 6),
                //             new AddonComponentPart("Feather4","Fourfold Feather 4", 7),
                //             new AddonComponentPart("Espirt","Espirt Gauge", 10),
                //             new AddonComponentPart("Espirt Value","Espirt Value", 8),
                //         }
                //     }
            }, {
                39,
                new JobGaugeUiMap(39, "Reaper") {
                    Addons = new() {
                        {
                            "JobHudRRP1",
                            new[] {
                                new AddonComponentPart("Shroud1", "Lemure Shroud 1", 21),
                                new AddonComponentPart("Shroud2", "Lemure Shroud 2", 20),
                                new AddonComponentPart("Shroud3", "Lemure Shroud 3", 19),
                                new AddonComponentPart("Shroud4", "Lemure Shroud 4", 18),
                                new AddonComponentPart("Shroud5", "Lemure Shroud 5", 17),
                                new AddonComponentPart("Enshroud", "Enshroud Icon", 15),
                                new AddonComponentPart("EnshroudIcon", "Enshroud Countdown", 16)
                            }
                        }, {
                            "JobHudRRP0",
                            new[] {
                                new AddonComponentPart("ShroudGauge", "Shroud Gauge", 45),
                                new AddonComponentPart("ShroudValue", "Shroud Gauge Value", 44),
                                new AddonComponentPart("DeathGauge", "Death Gauge", 42),
                                new AddonComponentPart("DeathValue", "Death Gauge Value", 41)
                            }
                        }
                    }
                }
            }, {
                40,
                new JobGaugeUiMap(40, "Sage") {
                    Addons = new() {
                        {
                            "JobHudGFF1",
                            new[] {
                                new AddonComponentPart("AddersgallGauge", "Addersgall Gauge", 34),
                                new AddonComponentPart("Addersgall1", "Addersgall 1", 27),
                                new AddonComponentPart("Addersgall2", "Addersgall 2", 28),
                                new AddonComponentPart("Addersgall3", "Addersgall 3", 29),
                                new AddonComponentPart("Addersting1", "Addersting 1", 31),
                                new AddonComponentPart("Addersting2", "Addersting 2", 32),
                                new AddonComponentPart("Addersting3", "Addersting 3", 33),
                            }
                        }, {
                            "JobHudGFF0",
                            new[] {
                                new AddonComponentPart("Eukrasia", "Eukrasia Icon", 17),
                            }
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
        
        public class JobConfig {
            public bool Enabled;
            public Dictionary<string, GaugeComponentConfig> Components = new();
        }

        public class Configs : TweakConfig {
            public Dictionary<uint, JobConfig> Jobs = new();
        }

        #endregion
    }
}