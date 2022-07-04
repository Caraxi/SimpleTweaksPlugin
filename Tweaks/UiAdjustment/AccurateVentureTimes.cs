using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class AccurateVentureTimes : UiAdjustments.SubTweak {
    public override string Name => "Accurate Venture Times";
    public override string Description => "Show live countdowns to venture completion on the retainer list.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Simple Display")]
        public bool SimpleDisplay = false;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    private delegate void UpdateRetainerDelegate(void* a1, int index, void* a3, void* a4);

    private UpdateRetainerDelegate updateRetainer;

    private void* updateRetainerPointer;
    private delegate void* AddonSetupDelegate(void* a1, AtkUnitBase* a2, void* a3);
    private HookWrapper<AddonSetupDelegate> addonSetupHook;

    private UpdateRetainerDelegate replacementUpdateRetainerDelegate;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        replacementUpdateRetainerDelegate = UpdateRetainerLineDetour;
        updateRetainerPointer = (void*) Service.SigScanner.ScanText("40 53 56 41 56 48 83 EC 30 48 8B B1");
        updateRetainer = Marshal.GetDelegateForFunctionPointer<UpdateRetainerDelegate>(new IntPtr(updateRetainerPointer));
        addonSetupHook ??= Common.Hook("E8 ?? ?? ?? ?? 41 B1 1E", new AddonSetupDelegate(SetupDetour));
        addonSetupHook?.Enable();
        Service.Framework.Update += FrameworkOnUpdate;
        base.Enable();
    }

    private void* SetupDetour(void* a1, AtkUnitBase* a2, void* a3) {
        if (a3 == updateRetainerPointer) {
            var ptr = Marshal.GetFunctionPointerForDelegate(replacementUpdateRetainerDelegate);
            a3 = (void*) ptr;
        }
        return addonSetupHook.Original(a1, a2, a3);
    }

    private void UpdateRetainerLineDetour(void* a1, int index, void* a3, void* a4) {
        updateRetainer(a1, index, a3, a4);
        try {
            UpdateRetainerList();
        } catch {
            //
        }
    }

    private bool UpdateRetainerList() {
        try {
            var addon = Common.GetUnitBase("RetainerList");
            if (addon == null) return false;
            var listNode = (AtkComponentNode*)addon->GetNodeById(27);
            if (listNode == null || (ushort)listNode->AtkResNode.Type < 1000) return false;
            var retainerManager = RetainerManager.Instance();
            for (uint i = 0; i < 10; i++) {
                var retainer = retainerManager->GetRetainerBySortedIndex(i);

                if (retainer->VentureComplete != 0) {
                    var renderer = Common.GetNodeByID<AtkComponentNode>(&listNode->Component->UldManager, i == 0 ? 4U : 41000U + i, (NodeType) 1011);
                    if (renderer == null || !renderer->AtkResNode.IsVisible) continue;
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

            return true;
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            return false;
        }
    }

    private readonly Stopwatch sw = new();

    private void FrameworkOnUpdate(Framework framework) {
        if (!sw.IsRunning) sw.Restart();
        if (sw.ElapsedMilliseconds >= 1000) {
            if (!UpdateRetainerList()) {
                sw.Restart();
            }
        }
    }

    private void CloseRetainerList() {
        var rl = Common.GetUnitBase("RetainerList");
        if (rl != null) UiHelper.Close(rl, true);
    }

    public override void Disable() {
        Service.Framework.Update -= FrameworkOnUpdate;
        addonSetupHook?.Disable();
        CloseRetainerList();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        addonSetupHook?.Dispose();
        base.Dispose();
    }
}