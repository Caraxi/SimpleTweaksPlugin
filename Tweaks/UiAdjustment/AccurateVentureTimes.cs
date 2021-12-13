using System;
using System.Collections.Generic;
using Dalamud;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class AccurateVentureTimes : UiAdjustments.SubTweak {
        public override string Name => "Accurate Venture Times";
        public override string Description => "Show live countdowns to venture completion on the retainer list.";

        public class Configs : TweakConfig {
            [TweakConfigOption("Simple Display")]
            public bool SimpleDisplay = false;
        }

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();
            Service.Framework.Update += FrameworkOnUpdate;
            base.Enable();
        }

        private int updateThrottle;

        private void FrameworkOnUpdate(Framework framework) {
            updateThrottle++;
            if (updateThrottle > 10) {
                updateThrottle = 0;
                var addon = Common.GetUnitBase("RetainerList");
                if (addon == null) return;
                var listNode = (AtkComponentNode*)addon->GetNodeById(24);
                if (listNode == null || (ushort)listNode->AtkResNode.Type < 1000) return;
                var retainerManager = RetainerManager.Instance();
                for (uint i = 0; i < 10; i++) {
                    var retainer = retainerManager->GetRetainerBySortedIndex(i);

                    if (retainer->VentureComplete != 0) {
                        var renderer = Common.GetNodeByID<AtkComponentNode>(listNode->Component->UldManager, i == 0 ? 4U : 41000U + i, (NodeType) 1011);
                        if (renderer == null || !renderer->AtkResNode.IsVisible) return;
                        var ventureText = (AtkTextNode*) renderer->Component->UldManager.SearchNodeById(12);
                        var cTime = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.GetServerTime();
                        var rTime = retainer->VentureComplete - cTime;

                        if (rTime <= 0) {
                            ventureText->SetText(Service.Data.Excel.GetSheet<Addon>()?.GetRow(12592)?.Text?.RawString ?? "Complete");
                        } else {

                            var tSpan = TimeSpan.FromSeconds(rTime);

                            if (Config.SimpleDisplay) {
                                if (tSpan.Hours > 0) {
                                    ventureText->SetText($"{tSpan.Hours:00}:{tSpan.Minutes:00}:{tSpan.Seconds:00}");
                                } else {
                                    ventureText->SetText($"{tSpan.Minutes:00}:{tSpan.Seconds:00}");
                                }
                            } else {
                                var timeString = new List<string>();
                                switch (Service.ClientState.ClientLanguage) {
                                    case ClientLanguage.Japanese:
                                        if (tSpan.Hours > 0) timeString.Add($"{tSpan.Hours}時間");
                                        if (tSpan.Minutes > 0) timeString.Add($"{tSpan.Minutes}分");
                                        timeString.Add($"{tSpan.Seconds}秒");
                                        ventureText->SetText($"残り時間{string.Join("", timeString)}");
                                        break;
                                    case ClientLanguage.German:
                                        if (tSpan.Hours > 0) timeString.Add($"{tSpan.Hours} Std");
                                        if (tSpan.Minutes > 0) timeString.Add($"{tSpan.Minutes} Min");
                                        timeString.Add($"{tSpan.Seconds} Sek");

                                        ventureText->SetText($"Complete in {string.Join(' ', timeString)}");
                                        break;
                                    case ClientLanguage.French:
                                        if (tSpan.Hours > 0) timeString.Add($"{tSpan.Hours}h");
                                        if (tSpan.Minutes > 0) timeString.Add($"{tSpan.Minutes}m");
                                        timeString.Add($"{tSpan.Seconds}s");
                                        ventureText->SetText($"Fin de la tâche dans {string.Join(' ', timeString)}");
                                        break;
                                    case ClientLanguage.English:
                                    default:
                                        if (tSpan.Hours > 0) timeString.Add($"{tSpan.Hours}h");
                                        if (tSpan.Minutes > 0) timeString.Add($"{tSpan.Minutes}m");
                                        timeString.Add($"{tSpan.Seconds}s");
                                        ventureText->SetText($"Complete in {string.Join(' ', timeString)}");
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void Disable() {
            Service.Framework.Update -= FrameworkOnUpdate;
            SaveConfig(Config);
            base.Disable();
        }
    }
}
