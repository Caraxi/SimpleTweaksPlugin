using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public unsafe class AdditionalItemInfo : TooltipTweaks.SubTweak {
    public override string Name => "Extra Information for Tooltips";
    public override string Description => "Adds extra information to item tooltips";
    public class Configs : TweakConfig {
        [TweakConfigOption("Craftable")]
        public bool Craftable = false;
        
        [TweakConfigOption("Grand Company Seal Value")]
        public bool GrandCompanySealValue = false;
        
        [TweakConfigOption("Gearsets")]
        public bool Gearsets = false;
        
        [TweakConfigOption("No Sell List", ConditionalDisplay = true)]
        public bool NoSellList = false;
        public bool ShouldShowNoSellList() => SimpleTweaksPlugin.Plugin.GetTweak<NoSellList>()?.Enabled ?? false;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    private HookWrapper<Common.AddonOnUpdate> itemTooltipOnUpdateHook;
    
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        itemTooltipOnUpdateHook ??= Common.Hook<Common.AddonOnUpdate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B AA", ItemTooltipUpdate);
        itemTooltipOnUpdateHook?.Enable();        
        base.Enable();
    }

    private void* ItemTooltipUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings) {
        BeforeItemDetailUpdate(atkUnitBase);
        var ret = itemTooltipOnUpdateHook.Original(atkUnitBase, nums, strings);
        AfterItemDetailUpdate(atkUnitBase, nums, strings);
        return ret;
    }

    private static List<(int gearsetId, string name)> GetGearSetsWithItem(uint itemId, bool hq) {
        var gearSetModule = RaptureGearsetModule.Instance();
        if (hq) itemId += 1000000;


        var l = new List<(int gearsetId, string name)>();
        for (var gs = 0; gs < 101; gs++) {
            var gearSet = gearSetModule->Gearset[gs];
            if (gearSet->ID != gs) break;
            if (!gearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            var gearSetItems = (RaptureGearsetModule.GearsetItem*)gearSet->ItemsData;
            for (var j = 0; j < 14; j++) {
                if (gearSetItems[j].ItemID == itemId) {
                    var name = Marshal.PtrToStringUTF8(new IntPtr(gearSet->Name));
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
            var recipes = Service.Data.Excel.GetSheet<ExtendedRecipeLookup>()?.GetRow(item.RowId);
            if (recipes != null) {
                var j = new List<string>();
                var c = new List<string>();
                for (var i = 0U; i < recipes.Recipes.Length; i++) {
                    var r = recipes.Recipes[i];
                    if (r == null || r.Row == 0 || r.Value?.CraftType?.Value?.Name == null) continue;
                    var cj = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(8 + i);
                    if (cj == null) continue;
                    j.Add(r.Value.CraftType.Value.Name.ToDalamudString().TextValue);
                    c.Add(cj.Abbreviation.ToDalamudString().TextValue);
                }

                str.AppendLine(j.Count == 8 ? $"Craftable - All" : j.Count >= 4 ? $"Craftable - {string.Join(",", c)}" : $"Craftable - {string.Join(", ", j)}");
            }
        }
        
        if (Config.GrandCompanySealValue) {
            if (item.Rarity > 1 && item.PriceLow > 0 && item.ClassJobCategory.Row > 0) {
                var gcSealValue = Service.Data.Excel.GetSheet<GCSupplyDutyReward>()?.GetRow(item.LevelItem.Row);
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
            var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(itemId % 500000);
            if (item != null) {
                return GetInfoLines(item, isHq, isCollectable);
            }
        } else {
            var item = Service.Data.Excel.GetSheet<EventItem>()?.GetRow(itemId);
            if (item != null) {
                return GetInfoLines(item);
            }
        }
        
        var str = new SeString();
        return str;
    }

    private void BeforeItemDetailUpdate(AtkUnitBase* atkUnitBase) {
        var textNode = Common.GetNodeByID<AtkTextNode>(&atkUnitBase->UldManager, CustomNodes.AdditionalInfo, NodeType.Text);
        if (textNode != null) {
            if (textNode->AtkResNode.IsVisible) {
                var insertNode = atkUnitBase->GetNodeById(2);
                if (insertNode == null) return;
                atkUnitBase->WindowNode->AtkResNode.SetHeight((ushort)(atkUnitBase->WindowNode->AtkResNode.Height - textNode->AtkResNode.Height));
                atkUnitBase->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(atkUnitBase->WindowNode->AtkResNode.Height);
                insertNode->SetPositionFloat(insertNode->X, insertNode->Y - textNode->AtkResNode.Height);
            }
        }
    }
    
    private void AfterItemDetailUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        if (!atkUnitBase->IsVisible) return;
        var textNode = Common.GetNodeByID<AtkTextNode>(&atkUnitBase->UldManager, CustomNodes.AdditionalInfo, NodeType.Text);
        if (textNode != null) textNode->AtkResNode.ToggleVisibility(false);
        if (Service.GameGui.HoveredItem > uint.MaxValue) return;
        
        var lines = GetInfoLines((uint)Service.GameGui.HoveredItem);
        if (lines.Payloads.Count == 0) return;

        var insertNode = atkUnitBase->GetNodeById(2);
        if (insertNode == null) return;

        var baseTextNode = atkUnitBase->GetTextNodeById(43);
        if (baseTextNode == null) return;
        
        if (textNode == null) {
            

            textNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
            if (textNode == null) return;
            textNode->AtkResNode.Type = NodeType.Text;
            textNode->AtkResNode.NodeID = CustomNodes.AdditionalInfo;
            
            
            textNode->AtkResNode.Flags = (short)(NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
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

    public override void Disable() {
        SaveConfig(Config);
        itemTooltipOnUpdateHook?.Disable();
        
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

    public override void Dispose() {
        itemTooltipOnUpdateHook?.Dispose();
        base.Dispose();
    }
}

