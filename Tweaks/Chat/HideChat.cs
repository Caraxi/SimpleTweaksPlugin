using System;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class HideChat : ChatTweaks.SubTweak {
    public override string Name => "Hide Chat";
    public override string Description => "Only show the chat windows and input box while typing.";

    private readonly string[] chatLogNodeNames = { "ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3" };

    public override void Enable() {
        Service.Framework.Update += FrameworkUpdate;
        ToggleVisibility(false);
        base.Enable();
    }
    public override void Disable() {
        Service.Framework.Update -= FrameworkUpdate;
        ToggleVisibility(true);
        base.Disable();
    }

    private void FrameworkUpdate (Framework framework)
    {
        try {
            // Visibility of chat logs are based on visibility of input cursor
            ToggleVisibility(GetChatInputCursorNode()->IsVisible);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private AtkResNode* GetChatInputCursorNode ()
    {
        var baseNode = Common.GetUnitBase("ChatLog");
        var textInputComponentNode = (AtkComponentNode*) baseNode->GetNodeById(5);
        var uldManager = &textInputComponentNode->Component->UldManager;
        return Common.GetNodeByID(uldManager, 2);
    }

    private void ToggleVisibility (bool visibility)
    {
        foreach (string name in chatLogNodeNames)
        {
            var node = Common.GetUnitBase(name);
            if (node == null) continue;
            node->RootNode->ToggleVisibility(visibility);
        };
    }
}
