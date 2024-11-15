using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Simplified Equipment Job Display")]
[TweakDescription("Hides classes from equipment tooltips when their jobs are unlocked.")]
[TweakReleaseVersion("1.8.3.0")]
[Changelog("1.8.4.0", "Fixed tweak for Japanese clients.")]
public unsafe class SimplifiedClassJobDisplay : TooltipTweaks.SubTweak {
    private Dictionary<string, ClassJob> abbrToClassJob = new();
    private Dictionary<string, (ClassJob, ClassJob)> replaceGroup = new();

    private string TooltipClassJobNameDisplay(ClassJob cj) => Service.ClientState.ClientLanguage == ClientLanguage.Japanese ? cj.Name.ExtractText() : cj.Abbreviation.ExtractText();

    protected override void Setup() {
        abbrToClassJob = Service.Data.Excel.GetSheet<ClassJob>()!.Where(cj => cj.ClassJobCategory.RowId != 0).ToDictionary(TooltipClassJobNameDisplay);
        replaceGroup = new Dictionary<string, (ClassJob, ClassJob)>();
        foreach (var cj in abbrToClassJob.Values) {
            if (cj.ClassJobParent.Value.RowId != 0 && cj.ClassJobParent.IsValid && cj.ExpArrayIndex == cj.ClassJobParent.Value.ExpArrayIndex) {
                replaceGroup.Add($"{TooltipClassJobNameDisplay(cj.ClassJobParent.Value)} {TooltipClassJobNameDisplay(cj)}", (cj.ClassJobParent.Value!, cj));
            }
        }
    }

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var str = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ClassJobCategory);
        if (str == null || str.Payloads.Count != 1 || str.Payloads.First() is not TextPayload textPayload || textPayload.Text == null) return;
        var split = textPayload.Text.Split(' ');
        var classJobs = split.Select(s => abbrToClassJob.GetValueOrDefault(s)).Where(cj => cj.RowId != 0).OrderBy(cj => cj.ExpArrayIndex).ThenBy(cj => cj.RowId).ToList();
        if (classJobs.Count != split.Length) return;
        var newStr = string.Join(' ', classJobs.Select(TooltipClassJobNameDisplay));
        foreach (var (key, (baseClass, job)) in replaceGroup) {
            newStr = newStr.Replace(key, QuestManager.IsQuestComplete(job.UnlockQuest.RowId) ? TooltipClassJobNameDisplay(job) : TooltipClassJobNameDisplay(baseClass));
        }

        try {
            SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ClassJobCategory, newStr);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }
}
