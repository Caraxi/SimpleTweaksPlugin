using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;
using System;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class HideChat : ChatTweaks.SubTweak
{
    public override string Name => "Hide Chat";
    public override string Description => $"Provides commands to hide the chat. ({Command} show|hide|toggle)";

    protected override string Author => "@dookssh @VariableVixen";

    // Note that since this is a chat SubTweak, we cannot inherit from CommandTweak
    private string Command = "/chatvis";
    private string HelpMessage => $"[{Plugin.Name} {Name}";
    private bool ShowInHelp = true;

    // If the plugin is failing, most likely you will need to update these IDs
    private readonly uint TextInputNodeID = 5;
    private readonly uint TextInputCursorID = 2;
    private readonly string[] ChatLogNodeNames = { "ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" };

    protected override void Enable()
    {
        Service.Commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = HelpMessage,
            ShowInHelp = ShowInHelp
        });

        Common.FrameworkUpdate += FrameworkUpdate;
        base.Enable();
    }

    protected override void Disable()
    {
        Service.Commands.RemoveHandler(Command);
        Common.FrameworkUpdate -= FrameworkUpdate;
        SetVisibility(true);
        base.Disable();
    }

    private void FrameworkUpdate()
    {
        try
        {
            // Always show chat when actively typing
            var inputCursorNode = GetChatInputCursorNode();
            if (inputCursorNode == null) return;
            if (inputCursorNode->IsVisible()) SetVisibility(true);
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
                break;

            case "hide":
                SetVisibility(false);
                break;

            case "toggle":
                var baseNode = Common.GetUnitBase("ChatLog");
                var isVisible = baseNode->RootNode->IsVisible();

                SetVisibility(!isVisible);
                break;

            default:
                break;
        }
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
