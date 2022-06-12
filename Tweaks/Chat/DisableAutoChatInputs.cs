using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class DisableAutoChatInputs : ChatTweaks.SubTweak {
    public override string Name => "Disable Auto Chat Inputs";
    public override string Description => "Prevent the game from inserting <flag> or other parameters into chat box.";
    
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

    private delegate byte InsertTextCommandParam(AgentInterface* agentChatLog, uint textCommandParam, byte a3);
    private HookWrapper<InsertTextCommandParam> insertTextCommandParamHook;

    public override bool UseAutoConfig => true;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        insertTextCommandParamHook ??= Common.Hook<InsertTextCommandParam>("E8 ?? ?? ?? ?? 40 88 6E 08 EB 04", InsertTextCommandParamDetour);
        insertTextCommandParamHook?.Enable();
        base.Enable();
    }

    private byte InsertTextCommandParamDetour(AgentInterface* agentChatLog, uint textCommandParam, byte a3) {
        var param = Service.Data.Excel.GetSheet<TextCommandParam>()?.GetRow(textCommandParam);
        if (param == null) return insertTextCommandParamHook.Original(agentChatLog, textCommandParam, a3);
        var text = param.Param.RawString;
        if (Config.DisableItem && text == "<item>") return 0;
        if (Config.DisableFlag && text == "<flag>") return 0;
        if (Config.DisableQuest && text == "<quest>") return 0;
        if (Config.DisablePartyFinder && text == "<pfinder>") return 0;
        if (Config.DisableStatus && text == "<status>") return 0;
        return insertTextCommandParamHook.Original(agentChatLog, textCommandParam, a3);
    }

    public override void Disable() {
        insertTextCommandParamHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        insertTextCommandParamHook?.Dispose();
        base.Dispose();
    }
}

