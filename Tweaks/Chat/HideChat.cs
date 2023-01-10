using Dalamud.Game;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class HideChat : ChatTweaks.SubTweak
{
    public class HideChatConfig : TweakConfig
    {
        public bool IsOnDemand = false;
    }

    public HideChatConfig Config { get; private set; }

    public override string Name => "Hide Chat";
    public override string Description => $"Provides commands to hide the chat. ({Command} show|toggle|hide)";

    protected override string Author => "@dookssh";

    // Note that since this is a chat SubTweak, we cannot inherit from CommandTweak
    private string Command = "/chatvis";
    private string HelpMessage => $"[{Plugin.Name} {Name}";
    private bool ShowInHelp = true;

    // If the plugin is failing, most likely you will need to update these IDs
    private readonly uint TextInputNodeID = 5;
    private readonly uint TextInputCursorID = 2;

    private readonly string[] ChatLogNodeNames = { "ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" };

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
    {
        hasChanged |= ImGui.Checkbox("Show on demand when typing.", ref Config.IsOnDemand);
        ImGui.Text("/chatvis show|hide|toggle is supported while this is active.");

        if (!hasChanged) return;
        SetOnDemand(Config.IsOnDemand);

        // reset back to visible
        if (!Config.IsOnDemand) SetVisibility(true);
    };

    public override void Enable()
    {
        Config = LoadConfig<HideChatConfig>() ?? new HideChatConfig();

        Service.Commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = HelpMessage,
            ShowInHelp = ShowInHelp
        });

        SetOnDemand(true);
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(Config);
        Service.Commands.RemoveHandler(Command);
        SetOnDemand(false);
        SetVisibility(true);
        base.Disable();
    }

    private void FrameworkUpdate(Framework framework)
    {
        try
        {
            var visibility = GetChatInputCursorNode()->IsVisible;
            SetVisibility(visibility);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    }

    private void OnCommand(string command, string args)
    {
        var argList = args.ToLower().Split(" ");

        switch (argList[0])
        {
            case "show":
                SetVisibility(true);
                if (Config.IsOnDemand) SetOnDemand(false);
                break;

            case "hide":
                SetVisibility(false);
                if (Config.IsOnDemand) SetOnDemand(true);
                break;

            case "toggle":
                var baseNode = Common.GetUnitBase("ChatLog");
                var isVisible = baseNode->RootNode->IsVisible;

                SetVisibility(!isVisible);
                if (Config.IsOnDemand) SetOnDemand(isVisible);
                break;

            default:
                break;
        }
    }

    private AtkResNode* GetChatInputCursorNode()
    {
        AtkUnitBase* baseNode = Common.GetUnitBase("ChatLog");
        AtkComponentNode* textInputComponentNode = (AtkComponentNode*)baseNode->GetNodeById(TextInputNodeID);
        AtkUldManager uldManager = textInputComponentNode->Component->UldManager;

        return Common.GetNodeByID(&uldManager, TextInputCursorID);
    }

    private void SetOnDemand(bool flag)
    {
        if (flag) Service.Framework.Update += FrameworkUpdate;
        else Service.Framework.Update -= FrameworkUpdate;
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
