using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using Lumina.Text;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Display EXP Gain Percentage of Level")]
[TweakAuthor("zajrik")]
[TweakDescription("Adds the percentage of your next level to exp gains in chat.")]
[TweakReleaseVersion("1.10.8.0")]
[Changelog("1.10.10.0", "Added support for Occult Crescent's phantom jobs.")]
[Changelog("1.10.10.0", "Added support for earning experience on jobs other than current job.")]
[Changelog("1.15.0.7", "Fixed log message being hidden after Dalamud update.")]
public unsafe class ExpGainLevelPercent : ChatTweaks.SubTweak {
    
    private void AppendPercent(ILogMessage logMessage, float gainedExp, float expToNext) {
        logMessage.PreventOriginal();
        var str = new SeStringBuilder();
        var pctOfNextLevel = gainedExp / expToNext * 100.0f;
        str.Append(logMessage.FormatLogMessageForDebugging());
        str.Append($" ({MathF.Round(pctOfNextLevel, 3)}%)");
        Service.Chat.Print(new XivChatEntry {
            Type = XivChatType.Progress,
            MessageBytes = str.ToArray(),
        });
    }

    [LogMessage(588, 589)]
    private void OnLogMessage(ILogMessage logMessage) {
        try {
            if (!logMessage.TryGetIntParameter(0, out var classJobId) || classJobId < 0 || !logMessage.TryGetIntParameter(1, out var gainedExp) || gainedExp <= 0) return;
            if (logMessage.SourceEntity is not { IsPlayer: true } e || e.HomeWorldId != Service.PlayerState.HomeWorld.RowId || !e.Name.ExtractText().Equals(Service.PlayerState.CharacterName, StringComparison.InvariantCultureIgnoreCase)) return;
            var classJob = classJobId == 0 ? Service.PlayerState.ClassJob.Value : Service.Data.GetExcelSheet<ClassJob>().GetRow((uint)classJobId);
            var playerJobLevel = Service.PlayerState.GetClassJobLevel(classJob);
            var expToNext = Service.Data.GetExcelSheet<ParamGrow>().GetRow((uint)playerJobLevel).ExpToNext;
            if (expToNext <= 0) return;
            AppendPercent(logMessage, gainedExp, expToNext);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Error parsing EXP Gained");
        }
    }

    [LogMessage(10953)]
    private void OnLogMessagePhantomExp(ILogMessage logMessage) {
        try {
            if (!logMessage.TryGetIntParameter(0, out var phantomJobId) || phantomJobId < 0 || !logMessage.TryGetIntParameter(1, out var gainedExp) || gainedExp <= 0) return;
            var queueEntry = (LogMessageQueueItem*)logMessage.Address;
            if (queueEntry->SourceKind != EntityRelationKind.LocalPlayer) return;
            var state = PublicContentOccultCrescent.GetState();
            if (state == null) return;
            var level = state->SupportJobLevels[phantomJobId];
            if (level == 0) return;
            var expToNext = Service.Data.GetSubrowExcelSheet<MKDGrowDataSJob>().GetSubrow((uint)phantomJobId, level).Unknown0;
            if (expToNext <= 0) return;
            AppendPercent(logMessage, gainedExp, expToNext);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Error parsing Phantom EXP Gained");
        }
    }
}
