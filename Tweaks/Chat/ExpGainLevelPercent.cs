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
class ExpGainLevelPercent : ChatTweaks.SubTweak {
    protected override void Enable() {
        Service.Chat.ChatMessage += OnChatMessage;
        base.Enable();
    }

    protected override void Disable() {
        Service.Chat.ChatMessage -= OnChatMessage;
        base.Disable();
    }

    private readonly Regex expDropRegex = new Regex(
        @"You gain ([0-9,]+) ?(?:\(\+[0-9,]+%\) )?(?:[a-zA-Z ]+? )?experience points\."
    );

    private unsafe void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled
    ) {
        // Don't modify message if it's not an exp drop
        if (!expDropRegex.IsMatch(message.TextValue)) {
            return;
        }

        Match match = expDropRegex.Match(message.TextValue);

        // Parse gained exp from message
        string gainedExpStr = match.Groups[1].ToString().Replace(",", string.Empty);
        int gainedExp = int.Parse(gainedExpStr);

        // Get next level exp threshold
        PlayerState player = UIState.Instance()->PlayerState;
        byte playerJob = player.CurrentClassJobId;
        byte playerJobIndex = Service.Data.GetExcelSheet<ClassJob>().GetRow(playerJob).JobIndex;
        short playerJobLevel = player.ClassJobLevels[playerJobIndex];
        int expToNext = Service.Data.GetExcelSheet<ParamGrow>().GetRow((uint) playerJobLevel).ExpToNext;

        // Calculate gained exp percentage of next level
        double pctOfNextLevel = Math.Round(((float) gainedExp / (float) expToNext) * 100.0, 2);

        // Add percentage payload to message
        message.Payloads.Add(new TextPayload($" ({pctOfNextLevel}%)"));
    }
}

