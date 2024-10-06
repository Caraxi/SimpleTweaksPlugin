using System;
using System.Diagnostics;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

// E8 ?? ?? ?? ?? 48 8B 4B 10 33 FF C6 83

[TweakName("Improved Crafting Log")]
[TweakDescription("Modifies the Synthesize button in the Crafting Log to switch job or stand up from the crafting position, allowing you to stop crafting without closing the crafting log.")]
[Changelog("1.8.2.1", "Fixed a potential crash in specific circumstances.")]
[Changelog("1.9.1.0", "Made attempt to fix some issues", "Tweak has been disabled for everyone and marked as experimental.")]
public unsafe class ImprovedCraftingLog : Tweak {
    private readonly Stopwatch removeFrameworkUpdateEventStopwatch = new();
    private bool standingUp;

    private delegate void* ClickSynthesisButton(AddonRecipeNote* a1, void* a2);

    private delegate*<RecipeNote*, void*> passThroughFunction;

    private delegate void* CancelCrafting(RecipeNote* recipeNote);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B 4B 10 33 FF C6 83", DetourName = nameof(CancelCraftingDetour))]
    private readonly HookWrapper<CancelCrafting> cancelCraftingHook = null!;

    [TweakHook, Signature("40 55 53 56 57 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 0F 48 8B 7D 7F", DetourName = nameof(ReceiveEventDetour))]
    private readonly HookWrapper<AtkUnitBase.Delegates.ReceiveEvent> receiveEventHook = null!;

    protected override void Enable() {
        passThroughFunction = (delegate*<RecipeNote*, void*>)Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 C3 48 8B 5C 24 ?? 48 83 C4 20 5D");

        Service.Commands.AddHandler("/stopcrafting", new CommandInfo(((_, _) => {
            if (Service.ClientState.LocalPlayer != null && !standingUp) {
                var localPlayer = (Character*)Service.ClientState.LocalPlayer.Address;
                var addon = Common.GetUnitBase("RecipeNote");
                if (addon != null) {
                    GetCraftReadyState(out var selectedRecipeId);
                    if (selectedRecipeId > 0 && localPlayer->Mode == CharacterModes.Crafting) {
                        ReopenCraftingLog();
                        return;
                    }
                }
            }

            Service.Chat.PrintError("You can't use that command right now.");
        })) { HelpMessage = "Stops crafting without closing the crafting log.", ShowInHelp = true });

        ForceUpdate();
    }

    private void* CancelCraftingDetour(RecipeNote* recipeNote) {
        if (standingUp) return passThroughFunction(recipeNote);
        return cancelCraftingHook!.Original(recipeNote);
    }

    private void ForceUpdate() {
        try {
            if (Common.GetUnitBase<AddonRecipeNote>(out var addon, "RecipeNote")) {
                CraftingLogUpdated(addon);
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private byte? GetGearsetForClassJob(uint cjId) {
        var gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++) {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset == null) continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (gearset->Id != i) continue;
            if (gearset->ClassJob == cjId) return gearset->Id;
        }

        return null;
    }

    private void ReceiveEventDetour(AtkUnitBase* unitBase, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
        try {
            if (eventType == AtkEventType.ButtonClick && eventParam == 13) {
                SimpleLog.Debug("Clicked Synthesise Button");
                if (ClickSynthesisButtonDetour()) return;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        receiveEventHook.Original(unitBase, eventType, eventParam, atkEvent, atkEventData);
    }

    private bool ClickSynthesisButtonDetour() {
        try {
            uint requiredClass = 0;

            var readyState = GetCraftReadyState(ref requiredClass, out var a);

            SimpleLog.Debug($"Craft Ready State: {readyState} : {requiredClass} : {a}");

            switch (readyState) {
                case CraftReadyState.AlreadyCrafting: {
                    if (Service.ClientState.LocalPlayer != null && !standingUp) {
                        ReopenCraftingLog();
                    } else {
                        return true;
                    }

                    break;
                }
                case CraftReadyState.WrongClass: {
                    SimpleLog.Debug("Searching for Gearset");
                    var gearset = GetGearsetForClassJob(requiredClass);
                    SimpleLog.Debug($"Gearset Index: {gearset}");
                    if (gearset != null) {
                        RaptureGearsetModule.Instance()->EquipGearset(gearset.Value);
                        return true;
                    }

                    Service.Chat.PrintError($"You have no saved gearset for {Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(requiredClass)?.Name?.RawString ?? $"{requiredClass}"}.");
                    return true;
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return false;
    }

    private void ForceUpdateFramework() {
        if (removeFrameworkUpdateEventStopwatch.ElapsedMilliseconds > 5000) {
            Common.FrameworkUpdate -= ForceUpdateFramework;
            removeFrameworkUpdateEventStopwatch.Stop();
        }

        if (standingUp == false || Service.ClientState.LocalPlayer == null) return;
        var localPlayer = (Character*)Service.ClientState.LocalPlayer.Address;
        if (localPlayer->Mode != CharacterModes.Crafting) {
            standingUp = false;
        }

        ForceUpdate();
    }

    public enum CraftReadyState {
        NotReady,
        Ready,
        WrongClass,
        AlreadyCrafting,
    }

    private CraftReadyState GetCraftReadyState(out ushort selectedRecipeId) {
        uint requiredClass = 0;
        return GetCraftReadyState(ref requiredClass, out selectedRecipeId);
    }

    private CraftReadyState GetCraftReadyState(ref uint requiredClass, out ushort selectedRecipeId) {
        selectedRecipeId = 0;
        if (Service.ClientState.LocalPlayer == null) return CraftReadyState.NotReady;
        var uiRecipeNote = RecipeNote.Instance();
        if (uiRecipeNote == null || uiRecipeNote->RecipeList == null) return CraftReadyState.NotReady;
        var selectedRecipe = uiRecipeNote->RecipeList->SelectedRecipe;
        if (selectedRecipe == null) return CraftReadyState.NotReady;
        selectedRecipeId = selectedRecipe->RecipeId;
        if (selectedRecipe->CraftType >= 8) return CraftReadyState.NotReady;
        requiredClass = uiRecipeNote->Jobs[selectedRecipe->CraftType];
        var requiredJob = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(requiredClass);
        if (requiredJob == null) return CraftReadyState.NotReady;
        if (Service.ClientState.LocalPlayer.ClassJob.Id == requiredClass) return CraftReadyState.Ready;
        var localPlayer = (Character*)Service.ClientState.LocalPlayer.Address;
        return localPlayer->Mode == CharacterModes.Crafting ? CraftReadyState.AlreadyCrafting : CraftReadyState.WrongClass;
    }

    [AddonPostRequestedUpdate("RecipeNote")]
    private void CraftingLogUpdated(AddonRecipeNote* addon) {
        SimpleLog.Verbose("Updating");
        var ready = GetCraftReadyState(out _);
        if (ready == CraftReadyState.NotReady) return;

        var buttonText = ready switch {
            CraftReadyState.Ready => Service.Data.Excel.GetSheet<Addon>()?.GetRow(1404)?.Text?.ToDalamudString(),
            CraftReadyState.WrongClass => GetWrongClassButtonText(),
            CraftReadyState.AlreadyCrafting => Service.Data.Excel.GetSheet<Addon>()?.GetRow(643)?.Text?.ToDalamudString(),
            _ => null
        };

        if (buttonText != null) {
            addon->SynthesizeButton->ButtonTextNode->SetText(buttonText.Encode());
        }
    }

    private void ReopenCraftingLog() {
        if (!Common.GetUnitBase("RecipeNote", out var addon)) return;
        var localPlayer = (Character*)(Service.ClientState.LocalPlayer?.Address ?? nint.Zero);
        if (localPlayer == null) return;

        GetCraftReadyState(out var selectedRecipeId);
        if (selectedRecipeId > 0 && localPlayer->Mode == CharacterModes.Crafting) {
            var agent = AgentRecipeNote.Instance();

            addon->Hide(true, true, 0);
            agent->OpenRecipeByRecipeId(selectedRecipeId);

            standingUp = true;
            removeFrameworkUpdateEventStopwatch.Restart();
            Common.FrameworkUpdate += ForceUpdateFramework;
        }
    }

    protected override void Disable() {
        Common.FrameworkUpdate -= ForceUpdateFramework;
        if (Common.GetUnitBase<AddonRecipeNote>(out var addon)) {
            var text = Service.Data.Excel.GetSheet<Addon>()?.GetRow(1404)?.Text?.ToDalamudString().Encode();
            if (text != null) {
                SimpleLog.Log($"Resetting Button Test: {(ulong)addon->SynthesizeButton->ButtonTextNode:X}");
                addon->SynthesizeButton->ButtonTextNode->NodeText.SetString(text);
            }
        }

        Service.Commands.RemoveHandler("/stopcrafting");
    }

    private string GetWrongClassButtonText() {
        switch (Service.ClientState.ClientLanguage) {
            case ClientLanguage.Japanese:
                return "スイッチ・ジョブ";
            case ClientLanguage.English:
                return "Switch Job";
            case ClientLanguage.German:
                return "Job wechseln";
            case ClientLanguage.French:
                return "Changer de Job";
            default:
                return "Switch Job";
        }
    }
}
