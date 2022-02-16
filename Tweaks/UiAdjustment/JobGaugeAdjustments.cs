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

        private static Dictionary<JobInfo, Dictionary<string, JobPieceMap[]>> JobMap = CreateJobMap();

        #region Helper Classes

        private class JobPieceMap {
            internal JobPieceMap(string configName, Func<Configs, HideAndOffsetConfig> getConfig, params uint[] nodeIds) {
                ConfigName = configName;
                GetConfig = getConfig;
                NodeIds = nodeIds;
            }

            internal string ConfigName { get; }
            internal uint[] NodeIds { get; }
            internal Func<Configs, HideAndOffsetConfig> GetConfig { get; }
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
        
        private static Dictionary<JobInfo, Dictionary<string, JobPieceMap[]>> CreateJobMap()
            => new() {
                {
                    new JobInfo(19, "Paladin"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudPLD0",
                            new[] {
                                new JobPieceMap("Hide Oath Bar", c => c.PLDOathBar, 18),
                                new JobPieceMap("Hide Oath Bar Text", c => c.PLDOathBarText, 17),
                                new JobPieceMap("Hide Iron Will Indicator", c => c.PLDIronWillIndicator, 15)
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
                                new JobPieceMap("Hide Chakra 1", c => c.MNKChakra1, 18),
                                new JobPieceMap("Hide Chakra 2", c => c.MNKChakra2, 19),
                                new JobPieceMap("Hide Chakra 3", c => c.MNKChakra3, 20),
                                new JobPieceMap("Hide Chakra 4", c => c.MNKChakra4, 21),
                                new JobPieceMap("Hide Chakra 5", c => c.MNKChakra5, 22)
                            }
                        },
                        {
                            "JobHudMNK0",
                            new[] {
                                new JobPieceMap("Hide Text", c => c.MNKText, 38),
                                new JobPieceMap("Hide Beast Chakra 1", c => c.MNKBeastChakra1, 34),
                                new JobPieceMap("Hide Beast Chakra 2", c => c.MNKBeastChakra2, 35),
                                new JobPieceMap("Hide Beast Chakra 3", c => c.MNKBeastChakra3, 36),
                                new JobPieceMap("Hide Lunar Nadi", c => c.MNKLunarNadi, 26),
                                new JobPieceMap("Hide Solar Nadi", c => c.MNKSolarNadi, 29),
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
                                new JobPieceMap("Hide Defiance Icon", c => c.WARDefiance, 14),
                                new JobPieceMap("Hide Beast Gauge", c => c.WARBeastBar, 17),
                                new JobPieceMap("Hide Beast Gauge Text", c => c.WARBarText, 16),
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
                                new JobPieceMap("Hide LotD Bar", c => c.DRGDragonGauge, 43),
                                new JobPieceMap("Hide LotD Bar Text", c => c.DRGDragonGaugeText, 42),
                                new JobPieceMap("Hide Gaze 1", c => c.DRGGaze1, 36),
                                new JobPieceMap("Hide Gaze 2", c => c.DRGGaze2, 37),
                                new JobPieceMap("Hide Firstmind 1", c => c.DRGMind1, 39),
                                new JobPieceMap("Hide Firstmind 2", c => c.DRGMind2, 40),
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
                                new JobPieceMap("Hide Song Bar", c => c.BRDSongBar, 99),
                                new JobPieceMap("Hide Song Countdown", c => c.BRDSongCountdown, 97),
                                new JobPieceMap("Hide Song Name", c => c.BRDSongName, 76),
                                new JobPieceMap("Hide Repertoire 1", c => c.BRDRepertoire1, 90, 94),
                                new JobPieceMap("Hide Repertoire 2", c => c.BRDRepertoire2, 91, 95),
                                new JobPieceMap("Hide Repertoire 3", c => c.BRDRepertoire3, 92, 96),
                                new JobPieceMap("Hide Repertoire 4", c => c.BRDRepertoire4, 93),
                                new JobPieceMap("Hide Soul Voice Bar", c => c.BRDSoulVoiceBar, 87),
                                new JobPieceMap("Hide Soul Voice Text", c => c.BRDSoulVoiceText, 86),
                                new JobPieceMap("Hide Mage's Coda", c => c.BRDMageCoda, 79, 82),
                                new JobPieceMap("Hide Army's Coda", c => c.BRDArmyCoda, 80, 84),
                                new JobPieceMap("Hide Wanderer's Coda", c => c.BRDWandererCoda, 71, 83)
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
                                new JobPieceMap("Hide Lily Bar", c => c.WHMLilyGauge, 38),
                                new JobPieceMap("Hide Lily 1", c => c.WHMLily1, 30),
                                new JobPieceMap("Hide Lily 2", c => c.WHMLily2, 31),
                                new JobPieceMap("Hide Lily 3", c => c.WHMLily3, 32),
                                new JobPieceMap("Hide Blood Lily 1", c => c.WHMBloodLily1, 34),
                                new JobPieceMap("Hide Blood Lily 2", c => c.WHMBloodLily2, 35),
                                new JobPieceMap("Hide Blood Lily 3", c => c.WHMBloodLily3, 36),
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
                                new JobPieceMap("Hide CountdownText", c => c.BLMCountdownText, 36),
                                new JobPieceMap("Hide Ice/Fire 1", c => c.BLMIceFire1, 42),
                                new JobPieceMap("Hide Ice/Fire 2", c => c.BLMIceFire2, 43),
                                new JobPieceMap("Hide Ice/Fire 3", c => c.BLMIceFire3, 44),
                                new JobPieceMap("Hide Heart 1", c => c.BLMUmbralHeart1, 38),
                                new JobPieceMap("Hide Heart 2", c => c.BLMUmbralHeart2, 39),
                                new JobPieceMap("Hide Heart 3", c => c.BLMUmbralHeart3, 40),
                                new JobPieceMap("Hide Polygot Gauge", c => c.BLMPolygotGauge, 48),
                                new JobPieceMap("Hide Polygot 1", c => c.BLMPolygot1, 46),
                                new JobPieceMap("Hide Polygot 2", c => c.BLMPolygot2, 47),
                                new JobPieceMap("Hide Endochan", c => c.BLMEndochan, 33),
                                new JobPieceMap("Hide Paradox Gauge", c => c.BLMParadoxGauge, 34)
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
                                new JobPieceMap("Hide Trance Gauge", c => c.SMNTranceGauge, 56),
                                new JobPieceMap("Hide Trance Countdown", c => c.SMNTranceCountdown, 55),
                                new JobPieceMap("Hide Ruby Arcanum", c => c.SMNRubyArcanum, 51),
                                new JobPieceMap("Hide Topaz Arcanum", c => c.SMNTopazArcanum, 52),
                                new JobPieceMap("Hide Emerald Arcanum", c => c.SMNEmeraldArcanum, 53),
                                new JobPieceMap("Hide Pet Countdown", c => c.SMNPetCountdown, 50),
                                new JobPieceMap("Hide Pet Icon", c => c.SMNPetIcon, 49),
                                new JobPieceMap("Hide Bahamut/Phoenix", c => c.SMNBahamutPheonix, 47),
                            }
                        },
                        {
                            "JobHudSMN0",
                            new[] {
                                new JobPieceMap("Hide Aetherflow 1", c => c.SMNAetherflow1, 12),
                                new JobPieceMap("Hide Aetherflow 2", c => c.SMNAetherflow2, 13),
                            }
                        }
                    }
                },
                {
                    // TODO: Text shift when 0-9, 10-99, 100
                    // 112 > 95 > 78
                    new JobInfo(28, "Scholar"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudSCH0",
                            new[] {
                                new JobPieceMap("Hide Faire Gauge", c => c.SCHFaireGauge, 32),
                                new JobPieceMap("Hide Faire Gauge Text", c => c.SCHFaireGaugeText, 31),
                                new JobPieceMap("Hide Seraph Icon", c => c.SCHSeraphIcon, 29),
                                new JobPieceMap("Hide Seraph Countdown", c => c.SCHSeraphCountdown, 30),
                            }
                        },
                        {
                            "JobHudACN0",
                            new[] {
                                new JobPieceMap("Hide Aetherflow 1", c => c.SCHAetherflow1, 8),
                                new JobPieceMap("Hide Aetherflow 2", c => c.SCHAetherflow2, 9),
                                new JobPieceMap("Hide Aetherflow 3", c => c.SCHAetherflow3, 10),
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
                                new JobPieceMap("Hide Huton Bar", c => c.NINHutonBar, 20),
                                new JobPieceMap("Hide Huton Bar Text", c => c.NINHutonBarCountdown, 19),
                                new JobPieceMap("Hide Huton Clock Icon", c => c.NINHutonClockIcon, 18)
                            }
                        },
                        {
                            "JobHudNIN0",
                            new[] {
                                new JobPieceMap("Hide Ninki Gauge", c => c.NINNinkiGauge, 19),
                                new JobPieceMap("Hide Ninki Gauge Text", c => c.NINNinkiGaugeText, 18)
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
                                new JobPieceMap("HideHeatBar", c => c.MCHHeatBar, 38),
                                new JobPieceMap("HideHeatText", c => c.MCHHeatText, 37),
                                new JobPieceMap("Hide Overheat Icon", c => c.MCHOverheatIcon, 36),
                                new JobPieceMap("Hide Overheat Text", c => c.MCHOverheatText, 35),
                                new JobPieceMap("Hide Battery Bar", c => c.MCHBatteryBar, 43),
                                new JobPieceMap("Hide Battery Text", c => c.MCHBatteryText, 42),
                                new JobPieceMap("Hide Queen Icon", c => c.MCHQueenIcon, 41),
                                new JobPieceMap("Hide Queen Icon Text", c => c.MCHQueenText, 40),
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
                                new JobPieceMap("Hide Grit Icon", c => c.DRKGritIcon, 15),
                                new JobPieceMap("Hide Blood Gauge", c => c.DRKBloodGauge, 18),
                                new JobPieceMap("Hide Blood Gauge Text", c => c.DRKBloodGaugeText, 17)
                            }
                        },
                        {
                            "JobHudDRK1",
                            new[] {
                                new JobPieceMap("Hide Darkside Gauge", c => c.DRKDarksideGauge, 27),
                                new JobPieceMap("Hide Darkside Gauge Text", c => c.DRKDarksideGaugeText, 26),
                                new JobPieceMap("Hide Dark Arts", c => c.DRKDarkArts, 24),
                                new JobPieceMap("Hide Living Shadow", c => c.DRKLivingShadow, 22),
                                new JobPieceMap("Hide Living Shadow Text", c => c.DRKLivingShadowCountdown, 23),
                            }
                        }
                    }
                },
                // {
                //     new JobInfo(33, "Astrologian"),
                //     new Dictionary<string, JobPieceMap[]> {
                //         {
                //             "JobHud",
                //             new[] {
                //                 new JobPieceMap("", c => c, ),
                //             }
                //         }
                //     }
                // },
                // {
                //     new JobInfo(34, "Samurai"),
                //     new Dictionary<string, JobPieceMap[]> {
                //         {
                //             "JobHud",
                //             new[] {
                //                 new JobPieceMap("", c => c, ),
                //             }
                //         }
                //     }
                // },
                {
                    new JobInfo(35, "Red Mage"),
                    new Dictionary<string, JobPieceMap[]> {
                        {
                            "JobHudRDM0",
                            new[] {
                                new JobPieceMap("Hide White Mana Bar", c => c.RDMWhiteManaBar, 38),
                                new JobPieceMap("Hide White Mana Text", c => c.RDMWhiteManaText, 25),
                                new JobPieceMap("Hide Black Mana Bar", c => c.RDMBlackManaBar, 39),
                                new JobPieceMap("Hide Black Mana Text", c => c.RDMBlackManaText, 26),
                                new JobPieceMap("Hide Status Indicator", c => c.RDMStatusIndicator, 35),
                                new JobPieceMap("Hide Mana Stacks", c => c.RDMManaStacks, 27), // TODO: Individual Stacks
                            }
                        }
                    }
                },
                // {
                //     new JobInfo(37, "Gunbreaker"),
                //     new Dictionary<string, JobPieceMap[]> {
                //         {
                //             "JobHud",
                //             new[] {
                //                 new JobPieceMap("", c => c, ),
                //             }
                //         }
                //     }
                // },
                // {
                //     new JobInfo(38, "Dancer"),
                //     new Dictionary<string, JobPieceMap[]> {
                //         {
                //             "JobHud",
                //             new[] {
                //                 new JobPieceMap("", c => c, ),
                //             }
                //         }
                //     }
                // },
                // {
                //     new JobInfo(39, "Reaper"),
                //     new Dictionary<string, JobPieceMap[]> {
                //         {
                //             "JobHud",
                //             new[] {
                //                 new JobPieceMap("", c => c, ),
                //             }
                //         }
                //     }
                // },
                // {
                //     new JobInfo(40, "Sage"),
                //     new Dictionary<string, JobPieceMap[]> {
                //         {
                //             "JobHud",
                //             new[] {
                //                 new JobPieceMap("", c => c, ),
                //             }
                //         }
                //     }
                // }
            };
        
        #endregion

        #region Configuration

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
            public HideAndOffsetConfig MCHBatteryText = new() { OffsetX  = 112, OffsetY = 6 };
            public HideAndOffsetConfig MCHHeatBar = new() { OffsetX  = 0, OffsetY = 26 };
            public HideAndOffsetConfig MCHHeatText = new() { OffsetX  = 112, OffsetY = 32 };

            public HideAndOffsetConfig MNKChakra1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig MNKChakra2 = new() { OffsetX = 18, OffsetY = 0 };
            public HideAndOffsetConfig MNKChakra3 = new() { OffsetX = 36, OffsetY = 0 };
            public HideAndOffsetConfig MNKChakra4 = new() { OffsetX = 54, OffsetY = 0 };
            public HideAndOffsetConfig MNKChakra5 = new() { OffsetX = 72, OffsetY = 0 };
            public HideAndOffsetConfig MNKText = new() { OffsetX = -10, OffsetY = 4 };
            public HideAndOffsetConfig MNKBeastChakra1 = new() { OffsetX = 8, OffsetY = 8 };
            public HideAndOffsetConfig MNKBeastChakra2 = new() { OffsetX = 38, OffsetY = 8 };
            public HideAndOffsetConfig MNKBeastChakra3 = new() { OffsetX = 68, OffsetY = 8 };
            public HideAndOffsetConfig MNKLunarNadi = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig MNKSolarNadi = new() { OffsetX = 20, OffsetY = 0 };
            
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

            public HideAndOffsetConfig WHMLilyGauge = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig WHMLily1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig WHMLily2 = new() { OffsetX = 18, OffsetY = 0 };
            public HideAndOffsetConfig WHMLily3 = new() { OffsetX = 36, OffsetY = 0 };
            public HideAndOffsetConfig WHMBloodLily1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig WHMBloodLily2 = new() { OffsetX = 18, OffsetY = 0 };
            public HideAndOffsetConfig WHMBloodLily3 = new() { OffsetX = 36, OffsetY = 0 };
            
            public HideAndOffsetConfig BLMCountdownText = new() { OffsetX = -17, OffsetY = 2 };
            public HideAndOffsetConfig BLMIceFire1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig BLMIceFire2 = new() { OffsetX = 19, OffsetY = 0 };
            public HideAndOffsetConfig BLMIceFire3 = new() { OffsetX = 38, OffsetY = 0 };
            public HideAndOffsetConfig BLMUmbralHeart1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig BLMUmbralHeart2 = new() { OffsetX = 19, OffsetY = 0 };
            public HideAndOffsetConfig BLMUmbralHeart3 = new() { OffsetX = 38, OffsetY = 0 };
            public HideAndOffsetConfig BLMPolygotGauge = new() { OffsetX = 30, OffsetY = 6 };
            public HideAndOffsetConfig BLMPolygot1 = new() { OffsetX = 182, OffsetY = -1 };
            public HideAndOffsetConfig BLMPolygot2 = new() { OffsetX = 202, OffsetY = -1 };
            public HideAndOffsetConfig BLMEndochan = new() { OffsetX = -10, OffsetY = 0 };
            public HideAndOffsetConfig BLMParadoxGauge = new() { OffsetX = 108, OffsetY = -16 };
            
            public HideAndOffsetConfig SMNAetherflow1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig SMNAetherflow2 = new() { OffsetX = 18, OffsetY = 0 };
            public HideAndOffsetConfig SMNTranceGauge = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig SMNTranceCountdown = new() { OffsetX = 112, OffsetY = 6 };
            public HideAndOffsetConfig SMNRubyArcanum = new() { OffsetX = 0, OffsetY = 4 };
            public HideAndOffsetConfig SMNTopazArcanum = new() { OffsetX = 18, OffsetY = 4 };
            public HideAndOffsetConfig SMNEmeraldArcanum = new() { OffsetX = 36, OffsetY = 4 };
            public HideAndOffsetConfig SMNPetCountdown = new() { OffsetX = 85, OffsetY = 0 };
            public HideAndOffsetConfig SMNPetIcon = new() { OffsetX = 63, OffsetY = 4 };
            public HideAndOffsetConfig SMNBahamutPheonix = new() { OffsetX = 0, OffsetY = -3 };
            
            public HideAndOffsetConfig SCHFaireGauge = new() { OffsetX = 0, OffsetY = 30 };
            public HideAndOffsetConfig SCHFaireGaugeText = new() { OffsetX = 95, OffsetY = 36 };
            public HideAndOffsetConfig SCHSeraphIcon = new() { OffsetX = 0, OffsetY = 4 };
            public HideAndOffsetConfig SCHSeraphCountdown = new() { OffsetX = 24, OffsetY = 0 };
            public HideAndOffsetConfig SCHAetherflow1 = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig SCHAetherflow2 = new() { OffsetX = 18, OffsetY = 0 };
            public HideAndOffsetConfig SCHAetherflow3 = new() { OffsetX = 36, OffsetY = 0 };
            
            public HideAndOffsetConfig NINHutonBar = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig NINHutonBarCountdown = new() { OffsetX = 112, OffsetY = 6 };
            public HideAndOffsetConfig NINHutonClockIcon = new() { OffsetX = -16, OffsetY = -1 };
            public HideAndOffsetConfig NINNinkiGauge = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig NINNinkiGaugeText = new() { OffsetX = 112, OffsetY = 6 };
            
            public HideAndOffsetConfig DRKGritIcon = new() { OffsetX = 0, OffsetY = -4 };
            public HideAndOffsetConfig DRKBloodGauge = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig DRKBloodGaugeText = new() { OffsetX = 112, OffsetY = 6 };
            public HideAndOffsetConfig DRKDarksideGauge = new() { OffsetX = 0, OffsetY = 0 };
            public HideAndOffsetConfig DRKDarksideGaugeText = new() { OffsetX = 112, OffsetY = 6 };
            public HideAndOffsetConfig DRKDarkArts = new() { OffsetX = 2, OffsetY = 38 };
            public HideAndOffsetConfig DRKLivingShadow = new() { OffsetX = 0, OffsetY = 6 };
            public HideAndOffsetConfig DRKLivingShadowCountdown = new() { OffsetX = 35, OffsetY = 0 };
            
            // public HideAndOffsetConfig AST = new() { OffsetX = 0, OffsetY = 0 };
            // public HideAndOffsetConfig AST = new() { OffsetX = 0, OffsetY = 0 };
            // public HideAndOffsetConfig AST = new() { OffsetX = 0, OffsetY = 0 };
            // public HideAndOffsetConfig AST = new() { OffsetX = 0, OffsetY = 0 };
            // public HideAndOffsetConfig AST = new() { OffsetX = 0, OffsetY = 0 };
            // public HideAndOffsetConfig AST = new() { OffsetX = 0, OffsetY = 0 };
        }

        #endregion

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
        private bool VisibilityAndOffsetEditor(string label, HideAndOffsetConfig config, HideAndOffsetConfig defConfig) {
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
            foreach (var jobInfo in JobMap.Keys) {
                hasChanged |= DrawConfigJobSection(jobInfo);
            }

            // hasChanged |= ImGui.ColorEdit4(LocString("HP Bar Color"), ref Config.HpColor);
            // hasChanged |= ImGui.ColorEdit4(LocString("MP Bar Color"), ref Config.MpColor);
            // hasChanged |= ImGui.ColorEdit4(LocString("GP Bar Color"), ref Config.GpColor);
            // hasChanged |= ImGui.ColorEdit4(LocString("CP Bar Color"), ref Config.CpColor);

            if (!hasChanged) return;
            
            UpdateCurrentJobBar(false, true);
            SaveConfig(Config);
        };
        private bool DrawConfigJobSection(JobInfo jobInfo) {
            var hasChanged = false;
            if (ImGui.CollapsingHeader(LocString(jobInfo.JobName))) {
                foreach (var piece in JobMap[jobInfo].Values.SelectMany(v => v)) {
                    hasChanged |= VisibilityAndOffsetEditor(LocString(piece.ConfigName), piece.GetConfig(Config), piece.GetConfig(DefaultConfig));
                }
            }
            ImGui.Dummy(new Vector2(2) * ImGui.GetIO().FontGlobalScale);

            return hasChanged;
        }

        private void OnFrameworkUpdate(Framework framework) {
            try {
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
            UpdateCurrentJob(reset ? DefaultConfig : Config, preview, job.Value);
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

        private void UpdateCurrentJob(Configs config, bool preview, uint job) {
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
                        UpdateNode(hudAddon->GetNodeById(nodeId), piece.GetConfig(config), preview);
                    }
                }
            }
        }
    }
}