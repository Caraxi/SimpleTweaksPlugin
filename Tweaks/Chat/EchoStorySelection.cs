using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Echo Story Selection")]
[TweakDescription("When given multiple choices during quests, print the selected option to chat.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.3.2")]
[TweakAutoConfig]
public unsafe class EchoStorySelection : ChatTweaks.SubTweak {
    private readonly List<string> options = [];
    
    public class Config : TweakConfig {
        [TweakConfigOption("Display Character Name")]
        public bool DisplayCharacterName = false;
        [TweakConfigOption("Don't include Aethernet")]
        public bool ExcludeAethernet = false;
    }

    public Config TweakConfig { get; private set; } = null!;
    
    [AddonPostSetup("CutSceneSelectString", "SelectString")]
    private void OnAddonSetup(AddonSetupArgs obj) {
        switch (obj) {
            case { AddonName: "CutSceneSelectString", Addon: not 0 }:
                GetAddonStrings(((AddonCutSceneSelectString*) obj.Addon)->OptionList);
                break;
            
            case { AddonName: "SelectString", Addon: not 0 } when Service.Condition[ConditionFlag.OccupiedInQuestEvent]:
                GetAddonStrings(((AddonSelectString*) obj.Addon)->PopupMenu.PopupMenu.List);
                break;
        }
    }
    
    [AddonFinalize("CutSceneSelectString", "SelectString")]
    private void OnAddonFinalize(AddonFinalizeArgs obj) {
        switch (obj) {
            case { AddonName: "CutSceneSelectString", Addon: not 0 }:
                PrintSelectedString(((AddonCutSceneSelectString*) obj.Addon)->OptionList);
                break;
            
            case { AddonName: "SelectString", Addon: not 0 } when Service.Condition[ConditionFlag.OccupiedInQuestEvent]:
                PrintSelectedString(((AddonSelectString*) obj.Addon)->PopupMenu.PopupMenu.List);
                break;
        }
    }

    private void GetAddonStrings(AtkComponentList* list) {
        if(list is null) return;
        
        options.Clear();
        
        foreach (var index in Enumerable.Range(0, list->ListLength)) {
            var listItemRenderer = list->ItemRendererList[index].AtkComponentListItemRenderer;
            if (listItemRenderer is null) continue;

            var buttonTextNode = listItemRenderer->AtkComponentButton.ButtonTextNode;
            if (buttonTextNode is null) continue;

            var buttonText = Common.ReadSeString(buttonTextNode->NodeText.StringPtr);

            options.Add(buttonText.TextValue);
        }
    }

    private void PrintSelectedString(AtkComponentList* list) {
        if (list is null) return;

        var selectedItem = list->SelectedItemIndex;
        if (selectedItem < 0 || selectedItem >= options.Count) return;

        if (TweakConfig.ExcludeAethernet && options[selectedItem] == " Aethernet.") return;

        var message = new SeStringBuilder()
            .AddText(options[selectedItem])
            .Build();

        var playerName = PlayerState.Instance()->CharacterNameString;
            
        var name = new SeStringBuilder()
            .AddUiForeground(TweakConfig.DisplayCharacterName ? playerName : "Story Selection", 62)
            .Build();

        Service.Chat.Print(new XivChatEntry {
            Type = XivChatType.NPCDialogue,
            Message = message,
            Name = name,
        });
    }
}