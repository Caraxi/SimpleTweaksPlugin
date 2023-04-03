using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class EchoStorySelection : ChatTweaks.SubTweak
{
    public override string Name => "Echo Story Selection";
    public override string Description => "When given multiple choices during quests, print the selected option to chat.";
    protected override string Author => "MidoriKami";

    private readonly List<string> options = new();
    
    public class Config : TweakConfig
    {
        [TweakConfigOption("Display Character Name")]
        public bool DisplayCharacterName = false;
    }

    public Config TweakConfig { get; private set; } = null!;

    public override bool UseAutoConfig => true;
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak("1.8.3.2");
        
        base.Setup();
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        Common.AddonSetup += OnAddonSetup;
        Common.AddonFinalize += OnAddonFinalize;
        base.Enable();
    }
    
    public override void Disable()
    {
        SaveConfig(TweakConfig);
        
        Common.AddonSetup -= OnAddonSetup;
        Common.AddonFinalize -= OnAddonFinalize;
        base.Disable();
    }
    
    private void OnAddonSetup(SetupAddonArgs obj)
    {
        switch (obj)
        {
            case { AddonName: "CutSceneSelectString", Addon: not null}:
                GetAddonStrings(((AddonCutSceneSelectString*) obj.Addon)->OptionList);
                break;
            
            case { AddonName: "SelectString", Addon: not null } when Service.Condition[ConditionFlag.OccupiedInQuestEvent]:
                GetAddonStrings(((AddonSelectString*) obj.Addon)->PopupMenu.PopupMenu.List);
                break;
        }
    }
    
    private void OnAddonFinalize(SetupAddonArgs obj)
    {
        switch (obj)
        {
            case { AddonName: "CutSceneSelectString", Addon: not null}:
                PrintSelectedString(((AddonCutSceneSelectString*) obj.Addon)->OptionList);
                break;
            
            case { AddonName: "SelectString", Addon: not null } when Service.Condition[ConditionFlag.OccupiedInQuestEvent]:
                PrintSelectedString(((AddonSelectString*) obj.Addon)->PopupMenu.PopupMenu.List);
                break;
        }
    }

    private void GetAddonStrings(AtkComponentList* list)
    {
        if(list is null) return;
        
        options.Clear();
        
        foreach (var index in Enumerable.Range(0, list->ListLength))
        {
            var listItemRenderer = list->ItemRendererList[index].AtkComponentListItemRenderer;
            if (listItemRenderer is null) continue;

            var buttonTextNode = listItemRenderer->AtkComponentButton.ButtonTextNode;
            if (buttonTextNode is null) continue;

            var buttonText = Common.ReadSeString(buttonTextNode->NodeText.StringPtr);

            options.Add(buttonText.TextValue);
        }
    }

    private void PrintSelectedString(AtkComponentList* list)
    {
        if (list is null) return;

        var selectedItem = list->SelectedItemIndex;
        if (selectedItem >= 0 && selectedItem < options.Count)
        {
            var selectedString = options[selectedItem];
        
            var message = new SeStringBuilder()
                .AddText(selectedString)
                .Build();

            var playerName = Common.ReadString(PlayerState.Instance()->CharacterName);
            
            var name = new SeStringBuilder()
                .AddUiForeground(TweakConfig.DisplayCharacterName ? playerName : "Story Selection", 62)
                .Build();

            var entry = new XivChatEntry
            {
                Type = XivChatType.NPCDialogue,
                Message = message,
                Name = name,
            };
        
            Service.Chat.PrintChat(entry);
        }
    }
    
    // Temp until ClientStructs update
    [StructLayout(LayoutKind.Explicit, Size = 0x248)]
    public struct AddonCutSceneSelectString
    {
        [FieldOffset(0x00)] public AtkUnitBase AtkUnitBase;

        [FieldOffset(0x230)] public AtkComponentList* OptionList;
        [FieldOffset(0x238)] public AtkComponentTextNineGrid* TextLabel;
    }
}