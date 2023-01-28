using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using Dalamud.Game.Command;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class HideChatAuto : ChatTweaks.SubTweak
{
    public class HideChatAutoConfig : TweakConfig
    {
        public bool Everywhere = true;
        public bool InInstance = false;
        public bool InCombat = false;
        public bool InCutscene = false;
    }

    public HideChatAutoConfig Config { get; private set; }

    // Note that since this is a chat SubTweak, we cannot inherit from CommandTweak
    private string Command = "/autochatvis";
    private string HelpMessage => $"[{Plugin.Name} {Name}";
    private bool ShowInHelp = true;
    public override string Name => "Hide Chat Automatically";
    public override string Description => $"Hides chat automatically except when typing.\n" +
                                          $"Provides command to temporarily enable/disable ({Command} enable|disable|toggle)";
    protected override string Author => "@dookssh";

    private bool Paused = false;
    // If the plugin is failing, most likely you will need to update these IDs
    private readonly uint TextInputNodeID = 5;
    private readonly uint TextInputCursorID = 2;
    private readonly string[] ChatLogNodeNames = { "ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" };

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
    {
        hasChanged |= ImGui.Checkbox("Everywhere", ref Config.Everywhere);

        if (Config.Everywhere) ImGui.BeginDisabled();
        hasChanged |= ImGui.Checkbox("In Instance", ref Config.InInstance);
        hasChanged |= ImGui.Checkbox("In Battle", ref Config.InCombat);
        hasChanged |= ImGui.Checkbox("In Cutscenes", ref Config.InCutscene);
        if (Config.Everywhere) ImGui.EndDisabled();

        if (hasChanged)
        {
            SetVisibility(true);
        }
    };

    public override void Enable()
    {
        Service.Commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = HelpMessage,
            ShowInHelp = ShowInHelp
        });
        
        Config = LoadConfig<HideChatAutoConfig>() ?? new HideChatAutoConfig();
        Service.Framework.Update += FrameworkUpdate;
        base.Enable();
    }

    public override void Disable()
    {
        Service.Commands.RemoveHandler(Command);
        SaveConfig(Config);
        Service.Framework.Update -= FrameworkUpdate;
        SetVisibility(true);
        base.Disable();
    }

    private void FrameworkUpdate(Framework framework)
    {
        try
        {
            if (Paused) return;
            
            // Always show chat when actively typing
            var inputCursorNode = GetChatInputCursorNode();
            if (inputCursorNode == null) return;

            var visibility = inputCursorNode->IsVisible;
            if (visibility)
            {
                Dalamud.Logging.PluginLog.Log(visibility.ToString());
                SetVisibility(true);
                return;
            }

            var isEverywhere = Config.Everywhere;
            var isInInstance = Config.InInstance && (
                Service.Condition[ConditionFlag.BoundByDuty] ||
                Service.Condition[ConditionFlag.BoundByDuty56] ||
                Service.Condition[ConditionFlag.BoundByDuty95]
            );
            var isInCutscene = Config.InCutscene && Service.Condition[ConditionFlag.OccupiedInCutSceneEvent];
            var isInCombat = Config.InCombat && Service.Condition[ConditionFlag.InCombat];

            if (isEverywhere || isInInstance || isInCombat || isInCutscene) SetVisibility(false);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    }

    private void OnCommand(string command, string args)
    {
        var argList = args.ToLower().Split(" ");

        Paused = argList[0] switch
        {
            "enable" => false,
            "unpause" => false,
            "pause" => true,
            "disable" => true,
            "toggle" => !Paused,
            _ => Paused
        };
    }
    
    private AtkResNode* GetChatInputCursorNode()
    {
        var baseNode = Common.GetUnitBase("ChatLog");
        if (baseNode == null) return null;

        var textInputComponentNode = (AtkComponentNode*)baseNode->GetNodeById(TextInputNodeID);
        if (textInputComponentNode == null) return null;

        var textInputBaseNode = textInputComponentNode->Component;
        if (textInputBaseNode == null) return null;

        AtkUldManager uldManager = textInputBaseNode->UldManager;

        return Common.GetNodeByID(&uldManager, TextInputCursorID);
    }

    private void SetVisibility(bool visibility)
    {
        foreach (string name in ChatLogNodeNames)
        {
            AtkUnitBase* node = Common.GetUnitBase(name);
            if (node == null) continue;
            node->RootNode->ToggleVisibility(visibility);
        };
    }
}
