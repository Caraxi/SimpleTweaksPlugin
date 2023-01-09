using System;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class HideChat : ChatTweaks.SubTweak
{
    public override string Name => "Hide Chat";
    public override string Description => "Only show the chat windows and input box while typing.";

    // If the plugin is failing, most likely you will need to update these IDs
    private readonly uint TextInputNodeID = 5;
    private readonly uint TextInputCursorID = 2;

    private readonly string[] ChatLogNodeNames = { "ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" };

    public override void Enable()
    {
        Service.Framework.Update += FrameworkUpdate;
        ToggleVisibility(false);
        base.Enable();
    }
    public override void Disable()
    {
        Service.Framework.Update -= FrameworkUpdate;
        ToggleVisibility(true);
        base.Disable();
    }

    private void FrameworkUpdate(Framework framework)
    {
        try
        {
            AtkResNode* inputCursor = GetChatInputCursorNode();
            ToggleVisibility(inputCursor->IsVisible);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    }

    private AtkResNode* GetChatInputCursorNode()
    {
        AtkUnitBase* baseNode = Common.GetUnitBase("ChatLog");
        AtkComponentNode* textInputComponentNode = (AtkComponentNode*)baseNode->GetNodeById(TextInputNodeID);
        AtkUldManager uldManager = textInputComponentNode->Component->UldManager;

        return Common.GetNodeByID(&uldManager, TextInputCursorID);
    }

    private void ToggleVisibility(bool visibility)
    {
        foreach (string name in ChatLogNodeNames)
        {
            AtkUnitBase* node = Common.GetUnitBase(name);
            if (node == null) continue;
            node->RootNode->ToggleVisibility(visibility);
        };
    }
}
