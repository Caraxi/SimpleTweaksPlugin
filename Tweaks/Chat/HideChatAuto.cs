using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using SimpleTweaksPlugin.Events;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Hide Chat Automatically")]
[TweakDescription("Hides chat automatically except when typing.")]
[TweakAuthor("@dookssh")]
[TweakAutoConfig]
public unsafe class HideChatAuto : ChatTweaks.SubTweak {
    public class HideChatAutoConfig : TweakConfig {
        public bool Everywhere = true;
        public bool InInstance;
        public bool InCombat;
        public bool InCutscene;
    }

    public HideChatAutoConfig Config { get; set; }

    // If the plugin is failing, most likely you will need to update these IDs
    private const uint TextInputNodeID = 5;
    private const uint TextInputCursorID = 2;
    private readonly string[] chatLogNodeNames = ["ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3"];

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("Everywhere", ref Config.Everywhere);

        if (Config.Everywhere) ImGui.BeginDisabled();
        hasChanged |= ImGui.Checkbox("In Instance", ref Config.InInstance);
        hasChanged |= ImGui.Checkbox("In Battle", ref Config.InCombat);
        hasChanged |= ImGui.Checkbox("In Cutscenes", ref Config.InCutscene);
        if (Config.Everywhere) ImGui.EndDisabled();

        if (hasChanged) {
            SetVisibility(true);
        }
    }

    protected override void Disable() {
        SetVisibility(true);
    }

    [FrameworkUpdate]
    private void FrameworkUpdate() {
        try {
            // Always show chat when actively typing
            var inputCursorNode = GetChatInputCursorNode();
            if (inputCursorNode == null) return;

            var visibility = inputCursorNode->IsVisible();
            if (visibility) {
                SetVisibility(true);
                return;
            }

            var isEverywhere = Config.Everywhere;
            var isInInstance = Config.InInstance && (Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] || Service.Condition[ConditionFlag.BoundByDuty95]);
            var isInCutscene = Config.InCutscene && Service.Condition[ConditionFlag.OccupiedInCutSceneEvent];
            var isInCombat = Config.InCombat && Service.Condition[ConditionFlag.InCombat];

            if (isEverywhere || isInInstance || isInCombat || isInCutscene) SetVisibility(false);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private AtkResNode* GetChatInputCursorNode() {
        var baseNode = Common.GetUnitBase("ChatLog");
        if (baseNode == null) return null;

        var textInputComponentNode = (AtkComponentNode*)baseNode->GetNodeById(TextInputNodeID);
        if (textInputComponentNode == null) return null;

        var textInputBaseNode = textInputComponentNode->Component;
        if (textInputBaseNode == null) return null;

        var uldManager = textInputBaseNode->UldManager;

        return Common.GetNodeByID(&uldManager, TextInputCursorID);
    }

    private void SetVisibility(bool visibility) {
        foreach (var name in chatLogNodeNames) {
            var node = Common.GetUnitBase(name);
            if (node == null || node->RootNode == null) continue;
            node->RootNode->ToggleVisibility(visibility);
        }
    }
}
