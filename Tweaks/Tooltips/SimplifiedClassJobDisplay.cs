using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public unsafe class SimplifiedClassJobDisplay : TooltipTweaks.SubTweak {
    public override string Name => "Simplified Equipment Job Display";
    public override string Description => "Hides classes from equipment tooltips when their jobs are unlocked.";

    private Dictionary<string, ClassJob> abbrToClassJob = new();

    private Dictionary<string, (ClassJob, ClassJob)> replaceGroup = new();

    private string TooltipClassJobNameDisplay(ClassJob cj) => Service.ClientState.ClientLanguage == ClientLanguage.Japanese ? cj.Name.RawString : cj.Abbreviation.RawString;
    
    public override void Setup() {
        AddChangelogNewTweak("1.8.3.0");
        AddChangelog("1.8.4.0", "Fixed tweak for Japanese clients.");
        
        abbrToClassJob = Service.Data.Excel.GetSheet<ClassJob>()!.Where(cj => cj.ClassJobCategory.Row != 0).ToDictionary(TooltipClassJobNameDisplay);
        replaceGroup = new Dictionary<string, (ClassJob, ClassJob)>();
        foreach (var cj in abbrToClassJob.Values) {
            if (cj.ClassJobParent.Row != 0 && cj.ClassJobParent.Value != null && cj.ExpArrayIndex == cj.ClassJobParent.Value.ExpArrayIndex) {
                replaceGroup.Add($"{TooltipClassJobNameDisplay(cj.ClassJobParent.Value)} {TooltipClassJobNameDisplay(cj)}", (cj.ClassJobParent.Value!, cj));
            }
        }
        base.Setup();
    }

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var str = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ClassJobCategory);
        if (str == null || str.Payloads.Count != 1 || str.Payloads.First() is not TextPayload textPayload || textPayload.Text == null) return;
        var split = textPayload.Text.Split(' ');
        var classJobs = split.Select(s => abbrToClassJob.TryGetValue(s, out var cj) ? cj : null).Where(cj => cj is not null).OrderBy(cj => cj.ExpArrayIndex).ThenBy(cj => cj.RowId).ToList();
        if (classJobs.Count != split.Length) return;
        var newStr = string.Join(' ', classJobs.Select(TooltipClassJobNameDisplay));
        foreach (var (key, (baseClass, job)) in replaceGroup) {
            newStr = newStr.Replace(key, QuestManager.IsQuestComplete(job.UnlockQuest.Row) ? TooltipClassJobNameDisplay(job) : TooltipClassJobNameDisplay(baseClass));
        }

        try {
            SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ClassJobCategory, newStr);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }
}
