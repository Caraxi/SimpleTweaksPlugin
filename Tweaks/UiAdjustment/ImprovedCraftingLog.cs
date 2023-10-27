using System;
using System.Diagnostics;
using Dalamud.Game.Command;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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
    
    [TweakHook, Signature("E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 33 D2", DetourName = nameof(ClickSynthesisButtonDetour))]
    private readonly HookWrapper<ClickSynthesisButton> clickSynthesisButton = null!;

    protected override void Enable() {
        passThroughFunction = (delegate*<RecipeNote*, void*>) Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 C3 48 8B 5C 24 ?? 48 83 C4 20 5D");
        
        Service.Commands.AddHandler("/stopcrafting", new CommandInfo(((_, _) => {
            if (Service.ClientState.LocalPlayer != null && !standingUp) {
                var localPlayer = (Character*) Service.ClientState.LocalPlayer.Address;
                var addon = Common.GetUnitBase("RecipeNote");
                if (addon != null) {
                    GetCraftReadyState(out var selectedRecipeId);
                    if (selectedRecipeId > 0 && localPlayer->Mode == Character.CharacterModes.Crafting) {
                        ReopenCraftingLog();
                        return;
                    }
                }
            }
            
            Service.Chat.PrintError("You can't use that command right now.");
        })) {
            HelpMessage = "Stops crafting without closing the crafting log.",
            ShowInHelp = true
        });
        
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
            if (gearset->ID != i) continue;
            if (gearset->ClassJob == cjId) return gearset->ID;
        }
        return null;
    }
    
    private void* ClickSynthesisButtonDetour(AddonRecipeNote* addon, void* a2) {
        try {

            uint requiredClass = 0;
            
            var readyState = GetCraftReadyState(ref requiredClass, out _);
            switch (readyState) {
                case CraftReadyState.AlreadyCrafting: {
                    if (Service.ClientState.LocalPlayer != null && !standingUp) {
                        ReopenCraftingLog();
                    } else {
                        return null;
                    }

                    break;
                }
                case CraftReadyState.WrongClass: {
                    var gearset = GetGearsetForClassJob(requiredClass);
                    if (gearset != null) {
                        ChatHelper.SendMessage($"/gearset change {gearset.Value + 1}");
                        return null;
                    } 

                    Service.Chat.PrintError($"You have no saved gearset for {Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(requiredClass)?.Name?.RawString ?? $"{requiredClass}"}.");
                    break;
                }
            }
        } catch {
            //
        }
        
        
        return clickSynthesisButton.Original(addon, a2);
    }

    private void ForceUpdateFramework() {
        if (removeFrameworkUpdateEventStopwatch.ElapsedMilliseconds > 5000) Common.FrameworkUpdate -= ForceUpdateFramework;
        ForceUpdate();
        if (standingUp == false || Service.ClientState.LocalPlayer == null) return;
        var localPlayer = (Character*) Service.ClientState.LocalPlayer.Address;
        if (localPlayer->Mode != Character.CharacterModes.Crafting) {
            standingUp = false;
        }
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
        var localPlayer = (Character*) Service.ClientState.LocalPlayer.Address;
        return localPlayer->Mode == Character.CharacterModes.Crafting ? CraftReadyState.AlreadyCrafting : CraftReadyState.WrongClass;
    }
    
    [AddonPostRequestedUpdate("RecipeNote")]
    private void CraftingLogUpdated(AddonRecipeNote* addon) {
        SimpleLog.Log("Updating");
        var ready = GetCraftReadyState(out _);
        if (ready == CraftReadyState.NotReady) return;

        var buttonText = ready switch {
            CraftReadyState.Ready => Service.Data.Excel.GetSheet<Addon>()?.GetRow(1404)?.Text?.ToDalamudString(),
            CraftReadyState.WrongClass => "Switch Job",
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
        if (selectedRecipeId > 0 && localPlayer->Mode == Character.CharacterModes.Crafting) {
            var agent = AgentRecipeNote.Instance();

            addon->Hide(true, true, 0);
            agent->OpenRecipeByRecipeId(selectedRecipeId);

            standingUp = true;
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
}
