using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;
using System;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Hide Chat")]
[TweakDescription("Provides commands to hide the chat. (/$[CustomOrDefaultCommand] show|hide|toggle)")]
[TweakAuthor("@dookssh @VariableVixen")]
[TweakKey($"{nameof(ChatTweaks)}@{nameof(HideChat)}")]
[TweakCategory(TweakCategory.Chat)]
public unsafe class HideChat : CommandTweak {
    protected override string Command => "/chatvis";

    // If the plugin is failing, most likely you will need to update these IDs
    private const uint TextInputNodeID = 5;
    private const uint TextInputCursorID = 2;
    private readonly string[] chatLogNodeNames = ["ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3"];

    protected override void DisableCommand() {
        SetVisibility(true);
    }

    [FrameworkUpdate]
    private void FrameworkUpdate() {
        try {
            // Always show chat when actively typing
            var inputCursorNode = GetChatInputCursorNode();
            if (inputCursorNode == null) return;
            if (inputCursorNode->IsVisible()) SetVisibility(true);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    protected override void OnCommand(string args) {
        var argList = args.ToLower().Split(" ");

        switch (argList[0]) {
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
        }
    }

    private AtkResNode* GetChatInputCursorNode() {
        var baseNode = Common.GetUnitBase("ChatLog");
        if (baseNode == null) return null;

        var textInputComponentNode = (AtkComponentNode*)baseNode->GetNodeById(TextInputNodeID);
        if (textInputComponentNode == null) return null;

        var textInputBaseNode = textInputComponentNode->Component;
        if (textInputBaseNode == null) return null;
        return Common.GetNodeByID(&textInputBaseNode->UldManager, TextInputCursorID);
    }

    private void SetVisibility(bool visibility) {
        foreach (string name in chatLogNodeNames) {
            var unitBase = Common.GetUnitBase(name);
            if (unitBase == null || unitBase->RootNode == null) continue;
            unitBase->RootNode->ToggleVisibility(visibility);
        }
    }
}
