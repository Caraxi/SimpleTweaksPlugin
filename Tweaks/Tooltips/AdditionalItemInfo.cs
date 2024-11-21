﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Extra Information for Tooltips")]
[TweakDescription("Adds extra information to item tooltips.")]
[TweakAutoConfig]
public unsafe class AdditionalItemInfo : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Craftable")]
        public bool Craftable;
        
        [TweakConfigOption("Grand Company Seal Value")]
        public bool GrandCompanySealValue;
        
        [TweakConfigOption("Gearsets")]
        public bool Gearsets;
        
        [TweakConfigOption("No Sell List", ConditionalDisplay = true)]
        public bool NoSellList;
        public bool ShouldShowNoSellList() => SimpleTweaksPlugin.Plugin.GetTweak<NoSellList>()?.Enabled ?? false;
        
        [TweakConfigOption("Additional Data", HelpText = "Shows the 'AdditionalData' field some items contain. This is likely only useful for developers.")]
        public bool AdditionalData;
    }

    public Configs Config { get; private set; }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
    }

    private static List<(int gearsetId, string name)> GetGearSetsWithItem(uint itemId, bool hq) {
        var gearSetModule = RaptureGearsetModule.Instance();
        if (hq) itemId += 1000000;


        var l = new List<(int gearsetId, string name)>();
        for (var gs = 0; gs < 101; gs++) {
            var gearSet = gearSetModule->GetGearset(gs);
            if (gearSet == null) continue;
            if (gearSet->Id != gs) break;
            if (!gearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            foreach (var item in gearSet->Items) {
                if (item.ItemId == itemId) {
                    var name = gearSet->NameString;
                    l.Add((gs, name));
                    break;
                }
            }
        }

        return l;
    }
    
    
    public SeString GetInfoLines(Item item, bool hq, bool collectable) {
        var str = new SeString();

        if (Config.Craftable) {

            if (Service.Data.GetExcelSheet<ExtendedRecipeLookup>().TryGetRow(item.RowId, out var recipes)) {
                var j = new List<string>();
                var c = new List<string>();
                for (var i = 0U; i < recipes.Recipes.Length; i++) {
                    var r = recipes.Recipes[i];
                    if (r.RowId == 0 || string.IsNullOrEmpty(r.Value.CraftType.Value.Name.ExtractText())) continue;
                    var cj = Service.Data.Excel.GetSheet<ClassJob>().GetRow(8 + i);
                    j.Add(r.Value.CraftType.Value.Name.ExtractText());
                    c.Add(cj.Abbreviation.ExtractText());
                }

                str.AppendLine(j.Count == 8 ? $"Craftable - All" : j.Count >= 4 ? $"Craftable - {string.Join(",", c)}" : $"Craftable - {string.Join(", ", j)}");
            }
        }
        
        if (Config.GrandCompanySealValue) {
            if (item.Rarity > 1 && item is { PriceLow: > 0, ClassJobCategory.RowId: > 0 }) {
                var gcSealValue = Service.Data.Excel.GetSheet<GCSupplyDutyReward>().GetRow(item.LevelItem.RowId);
                if (gcSealValue is { SealsExpertDelivery: > 0 }) {
                    
                    str.Append(new IconPayload(UIState.Instance()->PlayerState.GrandCompany switch {
                        1 => BitmapFontIcon.LaNoscea,
                        2 => BitmapFontIcon.BlackShroud,
                        3 => BitmapFontIcon.Thanalan,
                        _ => BitmapFontIcon.NoCircle
                    }));
                    
                    str.AppendLine(new TextPayload($"Seals: {gcSealValue.SealsExpertDelivery:N0}"));
                }
            }
        }

        if (Config.Gearsets) {
            var gearSets = GetGearSetsWithItem(item.RowId, hq);

            if (gearSets.Count == 1) {
                str.AppendLine($"Gearset: {gearSets[0].name}");
            } else if (gearSets.Count > 1) {

                var l = 10;
                var c = 0;
                
                str.Append($"Gearsets: ");

                foreach(var gearSet in gearSets) {
                    if (l + gearSet.name.Length > 55) {
                        str.AppendLine();
                        str.Append("                 ");
                        l = 10;
                        c = 0;
                    }

                    if (c > 0) {
                        str.Append(new UIForegroundPayload(3));
                        str.Append(",");
                        str.Append(UIForegroundPayload.UIForegroundOff);
                    }
                   
                    str.Append($"{gearSet.name}");
                    l += gearSet.name.Length + 1;
                    c++;
                }
                
                str.AppendLine();
                
            }
            
        }
        
        if (Config.NoSellList) {
            var tweak = SimpleTweaksPlugin.Plugin.GetTweak<NoSellList>();
            if (tweak is { Enabled: true }) {
                var config = tweak.Config;
                if (config.NoSellList.Contains(item.RowId) || config.CustomLists.Any(l => l.Enabled && l.NoSellList.Contains(item.RowId))) {
                    str.Append(new UIForegroundPayload(539));
                    str.Append($"Selling blocked by {tweak.Name}");
                    str.Append(UIForegroundPayload.UIForegroundOff);
                    str.AppendLine();
                }
            }
        }
        
        if (Config.AdditionalData) {
            if (item.AdditionalData.RowId > 0) {
                var typeName = ((Type?) item.AdditionalData.GetType().GetField("<rowType>P", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item.AdditionalData))?.Name ?? "Unknown";
                str.Append(new UIForegroundPayload(5));
                str.Append($"AdditionalData: {typeName}#{item.AdditionalData.RowId}");
                str.Append(UIForegroundPayload.UIForegroundOff);
                str.AppendLine();
            }
        }
        
        while (str.Payloads.Count > 0 && str.Payloads[^1].Type == PayloadType.NewLine) {
            str.Payloads.RemoveAt(str.Payloads.Count - 1);
        }
        
        return str;
    }
    
    public SeString GetInfoLines(EventItem item) {
        var str = new SeString();

        return str;
    }
    
    
    public SeString GetInfoLines(uint itemId) {
        var isEventItem = itemId > 2000000;
        
        var isCollectable = itemId is > 500000 and < 1000000;
        var isHq = itemId is > 1000000 and < 1500000;
        
        if (!isEventItem) {
            if (Service.Data.GetExcelSheet<Item>().TryGetRow(itemId % 500000, out var item)) {
                return GetInfoLines(item, isHq, isCollectable);
            }
        } else {
            if (Service.Data.GetExcelSheet<EventItem>().TryGetRow(itemId, out var item)) {
                return GetInfoLines(item);
            }
        }
        
        var str = new SeString();
        return str;
    }

    [AddonPreRequestedUpdate("ItemDetail")]
    private void BeforeItemDetailUpdate(AtkUnitBase* atkUnitBase) {
        var textNode = Common.GetNodeByID<AtkTextNode>(&atkUnitBase->UldManager, CustomNodes.AdditionalInfo, NodeType.Text);
        if (textNode != null) {
            if (textNode->AtkResNode.IsVisible()) {
                var insertNode = atkUnitBase->GetNodeById(2);
                if (insertNode == null) return;
                atkUnitBase->WindowNode->AtkResNode.SetHeight((ushort)(atkUnitBase->WindowNode->AtkResNode.Height - textNode->AtkResNode.Height));
                atkUnitBase->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(atkUnitBase->WindowNode->AtkResNode.Height);
                insertNode->SetPositionFloat(insertNode->X, insertNode->Y - textNode->AtkResNode.Height);
            }
        }
    }
    
    [AddonPostRequestedUpdate("ItemDetail")]
    private void AfterItemDetailUpdate(AtkUnitBase* atkUnitBase) {
        if (!atkUnitBase->IsVisible) return;
        var textNode = Common.GetNodeByID<AtkTextNode>(&atkUnitBase->UldManager, CustomNodes.AdditionalInfo, NodeType.Text);
        if (textNode != null) textNode->AtkResNode.ToggleVisibility(false);
        var lines = GetInfoLines(AgentItemDetail.Instance()->ItemId);
        if (lines.Payloads.Count == 0) return;

        var insertNode = atkUnitBase->GetNodeById(2);
        if (insertNode == null) return;

        var baseTextNode = atkUnitBase->GetTextNodeById(44);
        if (baseTextNode == null) return;
        
        if (textNode == null) {
            

            textNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
            if (textNode == null) return;
            textNode->AtkResNode.Type = NodeType.Text;
            textNode->AtkResNode.NodeId = CustomNodes.AdditionalInfo;
            
            
            textNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
            textNode->AtkResNode.DrawFlags = 0;
            textNode->AtkResNode.SetWidth(50);
            textNode->AtkResNode.SetHeight(20);
            
            textNode->AtkResNode.Color.A = baseTextNode->AtkResNode.Color.A;
            textNode->AtkResNode.Color.R = baseTextNode->AtkResNode.Color.R;
            textNode->AtkResNode.Color.G = baseTextNode->AtkResNode.Color.G;
            textNode->AtkResNode.Color.B = baseTextNode->AtkResNode.Color.B;

            textNode->TextColor.A = baseTextNode->TextColor.A;
            textNode->TextColor.R = baseTextNode->TextColor.R;
            textNode->TextColor.G = baseTextNode->TextColor.G;
            textNode->TextColor.B = baseTextNode->TextColor.B;
            
            textNode->EdgeColor.A = baseTextNode->EdgeColor.A;
            textNode->EdgeColor.R = baseTextNode->EdgeColor.R;
            textNode->EdgeColor.G = baseTextNode->EdgeColor.G;
            textNode->EdgeColor.B = baseTextNode->EdgeColor.B;

            textNode->LineSpacing = 18;
            textNode->AlignmentFontType = 0x00;
            textNode->FontSize = 12;
            textNode->TextFlags = (byte)((TextFlags)baseTextNode->TextFlags | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize);
            textNode->TextFlags2 = 0;
            
            var prev = insertNode->PrevSiblingNode;
            textNode->AtkResNode.ParentNode = insertNode->ParentNode;

            insertNode->PrevSiblingNode = (AtkResNode*)textNode;
            
            if (prev != null) prev->NextSiblingNode = (AtkResNode*)textNode;

            textNode->AtkResNode.PrevSiblingNode = prev;
            textNode->AtkResNode.NextSiblingNode = insertNode;

            atkUnitBase->UldManager.UpdateDrawNodeList();
        }
        
        textNode->AtkResNode.ToggleVisibility(true);
        textNode->SetText(lines.Encode());
        textNode->ResizeNodeForCurrentText();
        textNode->AtkResNode.SetPositionFloat(17, atkUnitBase->WindowNode->AtkResNode.Height - 10f);
        
        atkUnitBase->WindowNode->AtkResNode.SetHeight((ushort)(atkUnitBase->WindowNode->AtkResNode.Height + textNode->AtkResNode.Height));
        
        atkUnitBase->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(atkUnitBase->WindowNode->AtkResNode.Height);
        insertNode->SetPositionFloat(insertNode->X, insertNode->Y + textNode->AtkResNode.Height);
        
    }

    protected override void Disable() {
        SaveConfig(Config);
        
        var unitBase = Common.GetUnitBase("ItemDetail");
        if (unitBase != null) {
            var textNode = (AtkTextNode*) Common.GetNodeByID(&unitBase->UldManager, CustomNodes.AdditionalInfo, NodeType.Text);
            if (textNode != null) {
                if (textNode->AtkResNode.PrevSiblingNode != null)
                    textNode->AtkResNode.PrevSiblingNode->NextSiblingNode = textNode->AtkResNode.NextSiblingNode;
                if (textNode->AtkResNode.NextSiblingNode != null)
                    textNode->AtkResNode.NextSiblingNode->PrevSiblingNode = textNode->AtkResNode.PrevSiblingNode;
                unitBase->UldManager.UpdateDrawNodeList();
                textNode->AtkResNode.Destroy(true);
            }
        }
        
        
        base.Disable();
    }
}

