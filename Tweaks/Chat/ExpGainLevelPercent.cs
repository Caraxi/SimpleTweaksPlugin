using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Display EXP Gain Percentage of Level")]
[TweakAuthor("zajrik")]
[TweakDescription("Adds the percentage of your next level to exp gains in chat.")]
[TweakReleaseVersion("1.10.8.0")]
[Changelog("1.10.10.0", "Added support for Occult Crescent's phantom jobs.")]
[Changelog("1.10.10.0", "Added support for earning experience on jobs other than current job.")]
public unsafe partial class ExpGainLevelPercent : ChatTweaks.SubTweak {
    private const XivChatType ExperienceGainedChatMessageType = (XivChatType)2112;

    public Dictionary<string, Func<int>> ExpToNextMap = new();

    protected override void Enable() {
        Service.Chat.ChatMessage += OnChatMessage;

        foreach (var cj in Service.Data.GetExcelSheet<ClassJob>()) {
            if (ExpToNextMap.ContainsKey(cj.Name.ExtractText().ToLowerInvariant())) continue;
            ExpToNextMap.TryAdd(cj.Name.ExtractText().ToLowerInvariant(), () => {
                var player = UIState.Instance()->PlayerState;
                var playerJobLevel = player.ClassJobLevels[cj.ExpArrayIndex];
                return Service.Data.GetExcelSheet<ParamGrow>().GetRow((uint)playerJobLevel).ExpToNext;
            });
        }

        foreach (var pj in Service.Data.GetExcelSheet<MKDSupportJob>()) {
            if (ExpToNextMap.ContainsKey(pj.Name.ExtractText().ToLowerInvariant())) continue;
            ExpToNextMap.TryAdd(pj.Name.ExtractText().ToLowerInvariant(), () => {
                var state = PublicContentOccultCrescent.GetState();
                if (state == null) return 0;
                var level = state->SupportJobLevels[(int)pj.RowId];
                if (level == 0) return 0;
                return (int) Service.Data.GetSubrowExcelSheet<MKDGrowDataSJob>().GetSubrow(pj.RowId, level).Unknown0;
            });
        }
        
    }

    protected override void Disable() {
        Service.Chat.ChatMessage -= OnChatMessage;
    }

    [GeneratedRegex(@"You gain ([0-9,]+) ?(?:\(\+[0-9,]+%\) )?([a-zA-Z ]+? )?experience points\.")]
    private static partial Regex ExpGainedRegex();
    
    private readonly Regex expDropRegex = ExpGainedRegex();

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        // Don't modify messages if its not in the experience gain chat channel.
        if (type != ExperienceGainedChatMessageType) return;
        
        var match = expDropRegex.Match(message.TextValue);
        if (!match.Success) return;
        
        var classJobName = match.Groups[2].ToString().Trim();

        if (string.IsNullOrWhiteSpace(classJobName)) {
            classJobName = Service.Objects.LocalPlayer?.ClassJob.Value.Name.ExtractText() ?? string.Empty;
        }

        if (!ExpToNextMap.TryGetValue(classJobName.ToLowerInvariant(), out var getNextExpFunc)) {
            return;
        }
        
        // Parse gained exp from message
        var gainedExpStr = match.Groups[1].ToString().Replace(",", string.Empty);
        var gainedExp = int.Parse(gainedExpStr);
        
        // Get next level exp threshold
        var expToNext = getNextExpFunc();
        
        if (expToNext <= 0) return;
        
        // Calculate gained exp percentage of next level
        var pctOfNextLevel = Math.Round((double)gainedExp / expToNext * 100.0f, 2);

        // Add percentage payload to message
        message.Payloads.Add(new TextPayload($" ({pctOfNextLevel}%)"));
    }
}
