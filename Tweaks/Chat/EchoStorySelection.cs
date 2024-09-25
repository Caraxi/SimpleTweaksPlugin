using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Echo Story Selection")]
[TweakDescription("When given multiple choices during quests, print the selected option to chat.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.3.2")]
[TweakAutoConfig]
[Changelog(UnreleasedVersion, "Added option to hide some choices.")]
public unsafe class EchoStorySelection : ChatTweaks.SubTweak {
    public class Config : TweakConfig {
        [TweakConfigOption("Display Character Name")]
        public bool DisplayCharacterName;

        public List<string> IgnoredMessages = [];
    }

    [TweakConfig] public Config TweakConfig { get; private set; } = null!;

    private string newIgnoredMessageString = string.Empty;

    protected void DrawConfig() {
        if (!ImGui.CollapsingHeader($"Hidden Choices ({TweakConfig.IgnoredMessages.Count})###HiddenMessages_{nameof(EchoStorySelection)}")) return;
        using (ImRaii.PushIndent()) {
            for (var i = 0; i < TweakConfig.IgnoredMessages.Count; i++) {
                using (ImRaii.PushId($"ignoredMessage_{i}")) {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                        TweakConfig.IgnoredMessages.RemoveAt(i);
                        i--;
                        continue;
                    }

                    var ignoredMessage = TweakConfig.IgnoredMessages[i];

                    ImGui.SameLine();

                    if (ImGui.InputText($"##hiddenMessage_{i}", ref ignoredMessage, 512)) {
                        TweakConfig.IgnoredMessages[i] = ignoredMessage;
                    }
                }
            }

            using (ImRaii.Disabled(string.IsNullOrWhiteSpace(newIgnoredMessageString) || TweakConfig.IgnoredMessages.Contains(newIgnoredMessageString.Trim()))) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus) && !string.IsNullOrWhiteSpace(newIgnoredMessageString)) {
                    TweakConfig.IgnoredMessages.Add(newIgnoredMessageString.Trim());
                    newIgnoredMessageString = string.Empty;
                }
            }

            ImGui.SameLine();
            ImGui.InputText($"##newIgnoredMessage", ref newIgnoredMessageString, 512);
        }
    }

    [AddonFinalize("CutSceneSelectString", "SelectString")]
    private void OnAddonFinalize(AddonFinalizeArgs obj) {
        switch (obj) {
            case { AddonName: "CutSceneSelectString", Addon: not 0 }:
                PrintSelectedString(((AddonCutSceneSelectString*)obj.Addon)->OptionList);
                break;

            case { AddonName: "SelectString", Addon: not 0 } when Service.Condition[ConditionFlag.OccupiedInQuestEvent]:
                PrintSelectedString(((AddonSelectString*)obj.Addon)->PopupMenu.PopupMenu.List);
                break;
        }
    }

    private void PrintSelectedString(AtkComponentList* list) {
        if (list is null) return;

        var selectedItem = list->SelectedItemIndex;
        if (selectedItem < 0 || selectedItem >= list->ListLength) return;

        var listItemRenderer = list->ItemRendererList[selectedItem].AtkComponentListItemRenderer;
        if (listItemRenderer is null) return;

        var buttonTextNode = listItemRenderer->AtkComponentButton.ButtonTextNode;
        if (buttonTextNode is null) return;

        var buttonText = Common.ReadSeString(buttonTextNode->NodeText);

        if (TweakConfig.IgnoredMessages.Any(i => i.Trim()
                .Equals(buttonText.TextValue.Trim(), StringComparison.InvariantCultureIgnoreCase)))
            return;

        var message = new SeStringBuilder().AddText(buttonText.TextValue)
            .Build();

        var playerName = PlayerState.Instance()->CharacterNameString;

        var name = new SeStringBuilder().AddUiForeground(TweakConfig.DisplayCharacterName ? playerName : "Story Selection", 62)
            .Build();

        Service.Chat.Print(new XivChatEntry {
            Type = XivChatType.NPCDialogue,
            Message = message,
            Name = name,
        });
    }
}
