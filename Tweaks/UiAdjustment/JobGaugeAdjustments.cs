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
        
        private readonly Dictionary<uint, GaugeComponentTracking> trackedNodeValues = new();
        private List<IntPtr> trackedNodes;
        private int? jobChangeFrameCount;
        private uint? lastJobSet;
        
        private delegate byte AddonOnUpdate(AtkUnitBase* atkUnitBase);
        private Hook<AddonOnUpdate> onUpdateHook;
        
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
        
        #region Configuration UI

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var padding = ImGui.GetStyle().CellPadding.X;

            var allHeight = 300 * ImGuiHelpers.GlobalScale;
            var jobSelectorWidth = 150 * ImGuiHelpers.GlobalScale;
            var configWidth = availableWidth - padding * 2 - jobSelectorWidth;
            
            // Job List (Left)
            ImGui.BeginChild("JobGaugeAdjustments::JobList", new Vector2(jobSelectorWidth, allHeight), false);
            
            foreach (var jobInfo in jobMap.Keys.OrderBy(k => k.JobName)) {
                if (ImGui.Selectable(jobInfo.JobName, configSelectedJob == jobInfo.JobId))
                    configSelectedJob = jobInfo.JobId;
            }
                
            ImGui.EndChild();
            ImGui.SameLine();

            // Job Configuration (Right)
            var selectedJob = jobMap.Keys.FirstOrDefault(m => m.JobId == configSelectedJob);
            var rightFlags = selectedJob == null ? ImGuiWindowFlags.None : ImGuiWindowFlags.MenuBar;
            ImGui.BeginChild("JobGaugeAdjustments::Configuration", new Vector2(configWidth, allHeight), false, rightFlags);
            if (ImGui.BeginMenuBar())
            {
                ImGui.TextWrapped(LocString(selectedJob!.JobName));
                ImGui.EndMenuBar();
            }

            if (selectedJob != null) {
                var jobConfig = GetJobConfig(selectedJob.JobId);
                var wasEnabled = jobConfig.Enabled;
                hasChanged |= DrawConfigJobSection(selectedJob, jobConfig);

                if (lastJobSet.HasValue && lastJobSet == selectedJob.JobId) {
                    var forceReset = hasChanged && !jobConfig.Enabled && wasEnabled;
                    if (hasChanged)
                        UpdateJobGauges(selectedJob.JobId, forceReset);
                    
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

        private bool DrawConfigJobSection(JobInfo jobInfo, JobConfig jobConfig) {
            var hasChanged = false;

            if (jobInfo.ComingSoon) {
                ImGui.Text(LocString("Coming Soon!"));
                return false;
            }
            
            // Draw name + Enable flag, expand as needed
            hasChanged |= ImGui.Checkbox(LocString("Enable"), ref jobConfig.Enabled);
            if (!jobConfig.Enabled) return hasChanged;
            
            // TODO: Split out initialization of components
            var pieces = jobMap[jobInfo].Values.SelectMany(v => v).ToArray();
            for (var i = 0; i < pieces.Length; i++) {
                var componentConfig = GetComponentConfig(jobConfig, pieces[i].Key);
                hasChanged |= DrawEditorForPiece(LocString(pieces[i].ConfigName), componentConfig);
                
                if (i < pieces.Length - 1)
                    ImGui.Separator();
            }

            return hasChanged;
        }

        private bool DrawEditorForPiece(string label, GaugeComponentConfig config) {
            var hasChanged = false;
            
            ImGui.Text(label);
            ImGui.SameLine();
            
            ImGui.SetCursorPosX(320 * ImGuiHelpers.GlobalScale);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##resetOffsetX_{label}")) {
                config.OffsetX = 0;
                config.OffsetY = 0;
                config.Hide = false;
                hasChanged = true;
            }
            ImGui.PopFont();
            
            hasChanged |= ImGui.Checkbox(LocString("Hide") + $"###hide_{label}", ref config.Hide);
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
        
        #endregion
        
        private void DetachJob() {
            SimpleLog.Debug("Resetting Tracked Info");
            
            trackedNodes = new List<IntPtr>();
            trackedNodeValues.Clear();
            onUpdateHook?.Disable();
            onUpdateHook = null;
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
            trackedNodes = new List<IntPtr>();
            lastJobSet = job;
            
            UpdateCurrentJobBar(false, job);
        }
        private void UpdateTrackedNodes() {
            if (trackedNodes == null)
                return;

            foreach (var node in trackedNodes) {
                UpdateTrackedNode((AtkResNode*) node);
            }
        }

        private void UpdateCurrentJobBar(bool reset, uint? job = null) {
            job ??= Service.ClientState.LocalPlayer?.ClassJob.Id;
            if (job == null)
                return;
            
            SimpleLog.Debug($"Refresh called for job {job.Value.ToString()}");
            UpdateJobGauges(job.Value, reset);
        }
        
        private GaugeComponentTracking GetTrackedNode(AtkResNode* node) {
            if (node == null)
                return null;

            trackedNodeValues.TryGetValue(node->NodeID, out var tracking);
            if (tracking != null) 
                return tracking;
            
            var isHidden = (node->Flags & 0x10) != 0x10;
            SimpleLog.Debug($"node {node->NodeID} {(isHidden ? "SHOULD" : "SHOULD NOT ")} be hidden");
            
            tracking = new GaugeComponentTracking {
                DefaultX = (int) node->X,
                DefaultY = (int) node->Y,
                ShouldBeHidden = isHidden
            };
            trackedNodeValues[node->NodeID] = tracking;
            

            return tracking;
        }

        private void UpdateTrackedNode(AtkResNode* node) {
            if (node == null)
                return;
            
            var tracking = GetTrackedNode(node);

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

            var info = jobMap.Keys.FirstOrDefault(map => map.JobId == lastJobSet);
            if (info == null)
                return;
            
            var map = jobMap[info];
            // TODO: This is complete bullshit to find. Rework this once all bugs fixed
            var pieceName = map.First(m => m.Value.Any(v => v.NodeIds.Contains(node->NodeID))).Value.First(p => p.NodeIds.Contains(node->NodeID)).Key;
            // TODO: Class FindPieceByNodeId()
            var jobConfig = GetJobConfig(lastJobSet!.Value);
            
            ShowOrHideNode(node, jobConfig.Components[pieceName], tracking);
        }
        
        private void UpdateNode(AtkResNode* node, GaugeComponentConfig config, bool reset) {
            if (node == null) {
                SimpleLog.Error("Node not found to update.");
                return;
            }

            var isHidden = (node->Flags & 0x10) != 0x10;
            var tracking = GetTrackedNode(node);
            if (reset) {
                if (isHidden && !tracking.ShouldBeHidden)
                    node->Flags ^= 0x10;
                
                node->SetPositionFloat(tracking.DefaultX + tracking.AdditionalX, tracking.DefaultY + tracking.AdditionalY);
                return;
            }

            ShowOrHideNode(node, config, tracking);
            
            var intendedX = tracking.DefaultX + tracking.AdditionalX + config.OffsetX;
            var intendedY = tracking.DefaultY + tracking.AdditionalY + config.OffsetY;

            node->SetPositionFloat(intendedX, intendedY);
            tracking.LastX = intendedX;
            tracking.LastY = intendedY;
        }
        
        // TODO: Clean up this function
        private void ShowOrHideNode(AtkResNode* node, GaugeComponentConfig config, GaugeComponentTracking tracking) {
            var isHidden = (node->Flags & 0x10) != 0x10;

            if (config.Hide && !isHidden) {
                SimpleLog.Log($"HIDING NODE {node->NodeID}");
                node->Flags ^= 0x10;
            }

            if (config.Hide)
                return;

            if (isHidden && !tracking.ShouldBeHidden)
                node->Flags ^= 0x10;
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
            
            // TODO: Default all of this shit instead of mixing all the initialization with the draw loop
            var reset = forceReset || !jobConfig.Enabled;
            
            var info = jobMap.Keys.FirstOrDefault(map => map.JobId == job);
            if (info == null) {
                SimpleLog.Debug($"Job with Id {job} not found in map. Cannot update.");
                return;
            }
            
            var maps = jobMap[info];
            foreach (var addonMap in maps) {
                var hudAddon = Common.GetUnitBase(addonMap.Key);
                // TODO: Move hook creation out of here? Should happen with initialization
                if (onUpdateHook == null) {
                    onUpdateHook = new Hook<AddonOnUpdate>(new IntPtr(hudAddon->AtkEventListener.vfunc[39]), OnUpdate);
                    onUpdateHook.Enable();
                }

                if (hudAddon == null) {
                    SimpleLog.Debug($"Could not get base addon {addonMap.Key} for job {job} in render.");
                    return;
                }

                foreach (var piece in addonMap.Value) {
                    var componentConfig = GetComponentConfig(jobConfig, piece.Key);
                    
                    foreach (var nodeId in piece.NodeIds) {
                        
                        var node = hudAddon->GetNodeById(nodeId);
                        if (!trackedNodes.Contains((IntPtr) node))
                            trackedNodes.Add((IntPtr) node);
                        
                        UpdateNode(node, componentConfig, reset);
                    }
                }
            }
        }
        
        // TODO: Move me somewhere else?
        private byte OnUpdate(AtkUnitBase* atkunitbase) {
            var result = onUpdateHook!.Original(atkunitbase);
            if (lastJobSet.HasValue && !jobChangeFrameCount.HasValue) {
                UpdateTrackedNodes();
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
            internal JobInfo(uint jobId, string jobName, bool comingSoon = false) {
                JobId = jobId;
                JobName = jobName;
                ComingSoon = comingSoon;
            }
            internal string JobName { get; }
            internal uint JobId { get; }
            internal bool ComingSoon { get; }
        }
        
        #endregion

        #region Per-Job Mappings
        
        // TODO: Change how the mappings work so there are not such insane workarounds to get config/elements
        
        private readonly Dictionary<JobInfo, Dictionary<string, JobPieceMap[]>> jobMap =
            new() {
                {
                    // TODO: Get rid of JobInfo. Key to ID, replace Dict<str, piece[]> with class with name + piece[]
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
                                new JobPieceMap("Master", "Master Timeout", 39),
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
                                new JobPieceMap("LivingShadowValue","Living Shadow Value", 23)
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
                {
                    new JobInfo(38, "Dancer", true),
                    new Dictionary<string, JobPieceMap[]>() //{
                    //     {
                    //         "JobHudDNC0",
                    //         new[] {
                    //             new JobPieceMap("Waiting","Waiting Icon", 13),
                    //             new JobPieceMap("Standard","Standard Step Background", 14),
                    //             new JobPieceMap("StandardGlow","Step Diamonds", 12),
                    //             new JobPieceMap("StandardIcon","Standard Step Icon", 8),
                    //             new JobPieceMap("StandardText","Standard Step Countdown", 10, 11),
                    //             new JobPieceMap("Step1","Step 1", 3),
                    //             new JobPieceMap("Step2","Step 2", 4),
                    //             new JobPieceMap("Step3","Step 3", 5),
                    //             new JobPieceMap("Step4","Step 4", 38),
                    //             new JobPieceMap("StepHighlight","Current Step Highlight", 4),
                    //             
                    //             new JobPieceMap("TechnicalBkg","Technical Step Background", 15),
                    //         }
                    //     },
                    //     {
                    //         "JobHudDNC1",
                    //         new[] {
                    //             new JobPieceMap("Feather1","Fourfold Feather 1", 4),
                    //             new JobPieceMap("Feather2","Fourfold Feather 2", 5),
                    //             new JobPieceMap("Feather3","Fourfold Feather 3", 6),
                    //             new JobPieceMap("Feather4","Fourfold Feather 4", 7),
                    //             new JobPieceMap("Espirt","Espirt Gauge", 10),
                    //             new JobPieceMap("Espirt Value","Espirt Value", 8),
                    //         }
                    //     }
                    // }
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
                                new JobPieceMap("EnshroudIcon","Enshroud Countdown", 16)
                            }
                        },
                        {
                            "JobHudRRP0",
                            new[] {
                                new JobPieceMap("ShroudGauge","Shroud Gauge", 45),
                                new JobPieceMap("ShroudValue","Shroud Gauge Value", 44),
                                new JobPieceMap("DeathGauge","Death Gauge", 42),
                                new JobPieceMap("DeathValue","Death Gauge Value", 41)
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

        public class GaugeComponentTracking {
            public int DefaultX;
            public int DefaultY;
            public bool ShouldBeHidden;

            public int LastX;
            public int LastY;

            public int AdditionalX;
            public int AdditionalY;
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