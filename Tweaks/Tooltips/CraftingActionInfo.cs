using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Improved Crafting Action Tooltips")]
[TweakDescription("Adds calculated efficiency of crafting actions to tooltips.")]
[TweakAutoConfig]
public unsafe class CraftingActionInfo : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Show Results Preview")]
        public bool ShowResultsPreview;
    }

    public Configs Config { get; private set; }

    private DalamudLinkPayload? identifier;
    private string progressString;
    private string qualityString;
    
    protected override void Setup() {
        AddChangelog("1.8.1.1", "Fixed tweak not disabling correctly.");
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        progressString ??= Service.Data.Excel.GetSheet<Addon>().GetRow(213).Text.ExtractText();
        qualityString ??= Service.Data.Excel.GetSheet<Addon>().GetRow(216).Text.ExtractText();
        
        identifier = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.CraftingActionInfoIdentifier, (_, _) => { });

        if (Config.ShowResultsPreview) {
            Common.FrameworkUpdate += FrameworkUpdate;
        }
        base.Enable();
    }

    protected override void ConfigChanged() {
        Common.FrameworkUpdate -= FrameworkUpdate;
        if (Config.ShowResultsPreview) {
            Common.FrameworkUpdate += FrameworkUpdate;
        }
    }

    protected override void Disable() {
        SaveConfig(Config);
        Common.FrameworkUpdate -= FrameworkUpdate;
        PluginInterface.RemoveChatLinkHandler((uint) LinkHandlerId.CraftingActionInfoIdentifier);
        base.Disable();
    }

    private void SetGhost(AtkTextNode* textNode, AtkTextNode* maxTextNode, AtkComponentNode* gauge, uint addValue) {
        var mainBar = Common.GetNodeByID<AtkNineGridNode>(&gauge->Component->UldManager, 2);
        if (mainBar == null) return;

        if (!(uint.TryParse(textNode->NodeText.ToString(), out var current) && uint.TryParse(maxTextNode->NodeText.ToString(), out var max))) {
            return;
        }

        var newValue = Math.Min(max, current + addValue);
        var newPercentage = newValue / (float)max;
        
        var ghostBar = Common.GetNodeByID<AtkNineGridNode>(&gauge->Component->UldManager, CustomNodes.CraftingGhostBar);
        var ghostText = Common.GetNodeByID<AtkTextNode>(&gauge->Component->UldManager, CustomNodes.CraftingGhostText);

        if (ghostBar == null && addValue > 0) {
            
            var newGhostBar = (AtkNineGridNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkNineGridNode), 8);
            
            if (newGhostBar != null) {
                IMemorySpace.Memset(newGhostBar, 0, (ulong)sizeof(AtkNineGridNode));
                newGhostBar->Ctor();

                newGhostBar->AtkResNode.Type = NodeType.NineGrid;
                newGhostBar->PartsList = mainBar->PartsList;
                newGhostBar->PartId = mainBar->PartId;
                newGhostBar->TopOffset = mainBar->TopOffset;
                newGhostBar->BottomOffset = mainBar->BottomOffset;
                newGhostBar->LeftOffset = mainBar->LeftOffset;
                newGhostBar->RightOffset = mainBar->RightOffset;
                newGhostBar->BlendMode = mainBar->BlendMode;
                newGhostBar->PartsTypeRenderType = mainBar->PartsTypeRenderType;

                newGhostBar->AtkResNode.NodeId = CustomNodes.CraftingGhostBar;
                newGhostBar->AtkResNode.SetPositionShort(0, 2);
                newGhostBar->AtkResNode.ScaleX = 1;
                newGhostBar->AtkResNode.ScaleY = 1;
                newGhostBar->AtkResNode.Alpha_2 = 128;
                
                
                newGhostBar->AtkResNode.SetWidth((ushort) (240 * newPercentage)); // todo: calculate
                newGhostBar->AtkResNode.SetHeight(12);

                
                newGhostBar->AtkResNode.ToggleVisibility(true);
                
                newGhostBar->AtkResNode.ParentNode = mainBar->AtkResNode.ParentNode;
                newGhostBar->AtkResNode.NextSiblingNode = &mainBar->AtkResNode;
                newGhostBar->AtkResNode.PrevSiblingNode = mainBar->AtkResNode.PrevSiblingNode;
                if (mainBar->AtkResNode.PrevSiblingNode != null) {
                    mainBar->AtkResNode.PrevSiblingNode->NextSiblingNode = &newGhostBar->AtkResNode;
                }
               
                mainBar->AtkResNode.PrevSiblingNode = &newGhostBar->AtkResNode;
                gauge->Component->UldManager.UpdateDrawNodeList();
                
                ghostBar = newGhostBar;
            }
        }

        if (ghostText == null && addValue > 0) {
            var newGhostText = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
            if (newGhostText != null) {

                IMemorySpace.Memset(newGhostText, 0, (ulong)sizeof(AtkTextNode));
                newGhostText->Ctor();
                
                newGhostText->AtkResNode.Type = NodeType.Text;
                newGhostText->AtkResNode.NodeFlags = textNode->AtkResNode.NodeFlags;
                newGhostText->AtkResNode.DrawFlags = 0;
                newGhostText->AtkResNode.SetPositionFloat(textNode->AtkResNode.X, -4);
                newGhostText->AtkResNode.SetWidth(textNode->AtkResNode.Width);
                newGhostText->AtkResNode.SetHeight(textNode->AtkResNode.Height);
                
                newGhostText->SetText("9999");

                newGhostText->LineSpacing = textNode->LineSpacing;
                newGhostText->AlignmentFontType = textNode->AlignmentFontType;
                newGhostText->FontSize = textNode->FontSize;
                newGhostText->TextFlags = (byte) ((TextFlags)textNode->TextFlags | TextFlags.Edge);
                newGhostText->TextFlags2 = 0;

                newGhostText->AtkResNode.NodeId = CustomNodes.CraftingGhostText;

                newGhostText->AtkResNode.Color.A = 0xFF;
                newGhostText->AtkResNode.Color.R = 0xFF;
                newGhostText->AtkResNode.Color.G = 0xFF;
                newGhostText->AtkResNode.Color.B = 0xFF;
                
                newGhostText->TextColor.A = 0xFF;
                newGhostText->TextColor.R = 0xFF;
                newGhostText->TextColor.G = 0xFF;
                newGhostText->TextColor.B = 0xFF;

                newGhostText->EdgeColor.A = 0x55;
                newGhostText->EdgeColor.R = 0xF0;
                newGhostText->EdgeColor.G = 0xF0;
                newGhostText->EdgeColor.B = 0x00;

                newGhostText->AtkResNode.ParentNode = mainBar->AtkResNode.ParentNode;
                newGhostText->AtkResNode.NextSiblingNode = &mainBar->AtkResNode;
                newGhostText->AtkResNode.PrevSiblingNode = mainBar->AtkResNode.PrevSiblingNode;
                if (mainBar->AtkResNode.PrevSiblingNode != null) {
                    mainBar->AtkResNode.PrevSiblingNode->NextSiblingNode = &newGhostText->AtkResNode;
                }
               
                mainBar->AtkResNode.PrevSiblingNode = &newGhostText->AtkResNode;
                gauge->Component->UldManager.UpdateDrawNodeList();

                ghostText = newGhostText;
            }
        }
        

        if (ghostBar == null || ghostText == null) return;

        if (addValue > 0) {
            ghostBar->AtkResNode.ToggleVisibility(true);
            textNode->AtkResNode.ToggleVisibility(false);
            ghostBar->AtkResNode.SetWidth((ushort) (240 * newPercentage));
            
            ghostText->AtkResNode.ToggleVisibility(true);
            ghostText->SetText($"{newValue}");
        } else {
            ghostBar->AtkResNode.ToggleVisibility(false);
            textNode->AtkResNode.ToggleVisibility(true);
            ghostText->AtkResNode.ToggleVisibility(false);
        }
        
    }
    
    public void FrameworkUpdate() {
        var addon = Common.GetUnitBase<AddonSynthesis>("Synthesis");
        if (addon == null) return;

        var progressGaugeNode = (AtkComponentNode*) addon->AtkUnitBase.GetNodeById(54);
        var progressText = addon->CurrentProgress;
        var progressMaxText = addon->MaxProgress;
        
        var qualityGaugeNode = (AtkComponentNode*) addon->AtkUnitBase.GetNodeById(60);
        var qualityText = addon->CurrentQuality;
        var qualityMaxText = addon->MaxQuality;
        
        // Confirm Existence
        if (progressGaugeNode == null || progressText == null || progressMaxText == null || qualityGaugeNode == null || qualityText == null || qualityMaxText == null) return;
        
        // Confirm types
        progressGaugeNode = progressGaugeNode->AtkResNode.GetAsAtkComponentNode();
        qualityGaugeNode = qualityGaugeNode->AtkResNode.GetAsAtkComponentNode();
        if (progressGaugeNode == null || qualityGaugeNode == null) return;
        
        if (Service.GameGui.HoveredAction.ActionID != 0) {
            var result = GetActionResult(Service.GameGui.HoveredAction.ActionID);
            SetGhost(progressText, progressMaxText, progressGaugeNode, result.progress);
            SetGhost(qualityText, qualityMaxText, qualityGaugeNode, result.quality);
        } else {
            SetGhost(progressText, progressMaxText, progressGaugeNode, 0);
            SetGhost(qualityText, qualityMaxText, qualityGaugeNode, 0);
        }
    }

    private (uint progress, uint quality) GetActionResult(uint id) {
        
        var agent = AgentCraftActionSimulator.Instance();
        if (agent == null) return (0, 0);
        
        var progress = 0U;
        var quality = 0U;
        
        // Find Progress
        foreach (var p in agent->Progress)
        {
            if (p.ActionId == id)
            {
                progress = p.ProgressIncrease;
                break;
            }
        }

        foreach (var q in agent->Quality)
        {
            if (q.ActionId == id)
            {
                quality = q.QualityIncrease;
                break;
            }
        }

        return (progress, quality);
    }
    
    public override void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (identifier == null) return;
        
        var (progress, quality) = GetActionResult(Action.Id);

        if (progress == 0 && quality == 0) return;
        
        var descriptionString = GetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description);
        if (descriptionString.Payloads.Any(payload => payload is DalamudLinkPayload { CommandId: (uint)LinkHandlerId.CraftingActionInfoIdentifier })) return; // Don't append when it already exists.
        
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(identifier);
        descriptionString.Append(RawPayload.LinkTerminator);
        descriptionString.Append(NewLinePayload.Payload);

        if (progress > 0) {
            descriptionString.Append(new UIForegroundPayload(500));
            descriptionString.Append(new UIGlowPayload(7));
            descriptionString.Append(new TextPayload($"{progressString}: "));
            descriptionString.Append(new UIForegroundPayload(0));
            descriptionString.Append(new UIGlowPayload(0));
            descriptionString.Append($"{progress}");
            if (quality > 0) descriptionString.Append(NewLinePayload.Payload);
        }

        if (quality > 0) {
            descriptionString.Append(new UIForegroundPayload(500));
            descriptionString.Append(new UIGlowPayload(7));
            descriptionString.Append(new TextPayload($"{qualityString}: "));
            descriptionString.Append(new UIForegroundPayload(0));
            descriptionString.Append(new UIGlowPayload(0));
            descriptionString.Append($"{quality}");
        }
        try {
            SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, descriptionString);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            Plugin.Error(this, ex);
        }
    }
}

