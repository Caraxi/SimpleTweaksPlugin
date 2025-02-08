using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Display EXP Gain Percentage of Level")]
[TweakAuthor("zajrik")]
[TweakDescription("Adds the percentage of your next level to exp gains in chat.")]
[TweakReleaseVersion(UnreleasedVersion)]
public partial class ExpGainLevelPercent : ChatTweaks.SubTweak {
    private const XivChatType ExperienceGainedChatMessageType = (XivChatType)2112;

    protected override void Enable() {
        Service.Chat.ChatMessage += OnChatMessage;
    }

    protected override void Disable() {
        Service.Chat.ChatMessage -= OnChatMessage;
    }

    [GeneratedRegex(@"You gain ([0-9,]+) ?(?:\(\+[0-9,]+%\) )?(?:[a-zA-Z ]+? )?experience points\.")]
    private static partial Regex ExpGainedRegex();
    
    private readonly Regex expDropRegex = ExpGainedRegex();

    private unsafe void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        // Don't modify messages if its not in the experience gain chat channel.
        if (type != ExperienceGainedChatMessageType) return;

        var match = expDropRegex.Match(message.TextValue);
        if (!match.Success) return;

        // Parse gained exp from message
        var gainedExpStr = match.Groups[1].ToString().Replace(",", string.Empty);
        var gainedExp = int.Parse(gainedExpStr);

        // Get next level exp threshold
        var player = UIState.Instance()->PlayerState;
        var playerJob = player.CurrentClassJobId;
        var playerJobIndex = Service.Data.GetExcelSheet<ClassJob>().GetRow(playerJob).ExpArrayIndex;
        var playerJobLevel = player.ClassJobLevels[playerJobIndex];
        var expToNext = Service.Data.GetExcelSheet<ParamGrow>().GetRow((uint)playerJobLevel).ExpToNext;

        // Calculate gained exp percentage of next level
        var pctOfNextLevel = Math.Round((double)gainedExp / expToNext * 100.0f, 2);

        // Add percentage payload to message
        message.Payloads.Add(new TextPayload($" ({pctOfNextLevel}%)"));
    }
}
