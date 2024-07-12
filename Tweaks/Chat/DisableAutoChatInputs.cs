using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Disable Auto Chat Inputs")]
[TweakDescription("Prevent the game from inserting <flag> or other parameters into chat box.")]
[TweakAutoConfig]
[Changelog("1.8.7.2", "Fixed game crash in 6.4")]
public unsafe class DisableAutoChatInputs : ChatTweaks.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Disable <item> when linking items.")]
        public bool DisableItem = false;

        [TweakConfigOption("Disable <status> when linking a status.")]
        public bool DisableStatus = false;

        [TweakConfigOption("Disable <flag> when setting map flags.")]
        public bool DisableFlag = false;

        [TweakConfigOption("Disable <quest> when linking quests.")]
        public bool DisableQuest = false;

        [TweakConfigOption("Disable <pfinder> when linking party finder.")]
        public bool DisablePartyFinder = false;
    }

    public Configs Config { get; private set; }

    [TweakHook(typeof(AgentChatLog), nameof(AgentChatLog.InsertTextCommandParam), nameof(InsertTextCommandParamDetour))]
    private HookWrapper<AgentChatLog.Delegates.InsertTextCommandParam> insertTextCommandParamHook;

    private bool InsertTextCommandParamDetour(AgentChatLog* agentChatLog, uint textCommandParam, bool a3) {
        var param = Service.Data.Excel.GetSheet<TextCommandParam>()?.GetRow(textCommandParam);
        if (param == null) return false; // Sanity Check
        var text = param.Param.RawString;
        return text switch {
            "<item>" when Config.DisableItem => false,
            "<flag>" when Config.DisableFlag => false,
            "<quest>" when Config.DisableQuest => false,
            "<pfinder>" when Config.DisablePartyFinder => false,
            "<status>" when Config.DisableStatus => false,
            _ => insertTextCommandParamHook.Original(agentChatLog, textCommandParam, a3)
        };
    }
}
