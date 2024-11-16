using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Text;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Market Enhancements")]
[TweakDescription("Highlight items that could be bought from, or sold to, an NPC for a better price.")]
[TweakAutoConfig]
[Changelog("1.8.9.0", "Return of the Lazy Tax/Profitable highlighting")]
public unsafe class MarketEnhancements : UiAdjustments.SubTweak {
    public class MarketEnhancementsConfig : TweakConfig {
        [TweakConfigOption("##ResellProfit", 1)]
        public bool HighlightNpcSellProfit;

        [TweakConfigOption("Highlight items that can be sold to NPC for more.", "SimpleColor", 2, SameLine = true)]
        public Vector4 NpcSellProfitColour = new(0, 1, 0, 1);

        [TweakConfigOption("##LazyTax", 3)] public bool HighlightLazyTax;

        [TweakConfigOption("Highlight items that can be purchased from an NPC for cheaper.", "SimpleColor", 4, SameLine = true)]
        public Vector4 LazyTaxColour = new(1, 0, 0, 1);
    }

    [TweakConfig] public MarketEnhancementsConfig Config { get; private set; }

    private delegate void UpdateResultDelegate(AtkUnitBase* addonItemSearchResult, uint a2, ulong* a3, void* a4);

    private UpdateResultDelegate updateResultOriginal;
    [Signature("40 57 48 83 EC 30 8B FA")] private nint updateResult;

    private delegate void* ListSetup(void* a1, AtkUnitBase* a2, nint a3);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 41 B1 08", DetourName = nameof(ListSetupDetour))]
    private HookWrapper<ListSetup> addonSetupHook;

    private UpdateResultDelegate replacementUpdateResultDelegate;

    protected override void AfterEnable() => UpdateItemList();

    private void* ListSetupDetour(void* a1, AtkUnitBase* a2, nint a3) {
        if (a3 == updateResult) {
            updateResultOriginal = Marshal.GetDelegateForFunctionPointer<UpdateResultDelegate>(a3);
            replacementUpdateResultDelegate ??= SetItemDetour;
            a3 = Marshal.GetFunctionPointerForDelegate(replacementUpdateResultDelegate);
        }

        return addonSetupHook.Original(a1, a2, a3);
    }

    private void SetItemDetour(AtkUnitBase* addonItemSearchResult, uint resultIndex, ulong* a3, void* a4) {
        try {
            updateResultOriginal(addonItemSearchResult, resultIndex, a3, a4);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        if (Enabled) UpdateItemList(addonItemSearchResult);
    }

    private uint npcPriceId;
    private uint npcBuyPrice;
    private uint npcSellPrice;

    private void UpdateItemList(AtkUnitBase* itemSearchResult = null) {
        try {
            if (itemSearchResult == null) itemSearchResult = Common.GetUnitBase("ItemSearchResult");
            if (itemSearchResult == null) return;
            if (itemSearchResult->NameString != "ItemSearchResult") return;

            var listNode = (AtkComponentNode*)itemSearchResult->UldManager.SearchNodeById(26);
            if (listNode == null) return;
            var component = (AtkComponentList*)listNode->Component;

            var agent = AgentItemSearch.Instance();
            if (agent == null) return;

            if (npcPriceId != agent->ResultItemId) {
                var item = Service.Data.Excel.GetSheet<Item>().GetRowOrNull(agent->ResultItemId);
                if (item == null) return;
                npcPriceId = agent->ResultItemId;
                npcBuyPrice = 0;
                npcSellPrice = item.Value.PriceLow;
                if (item.Value.ItemUICategory.RowId is 58) {
                    npcSellPrice += (uint)MathF.Ceiling(npcSellPrice * 0.1f);
                }

                var gilShopItem = Service.Data.Excel.GetSubrowSheet<GilShopItem>().Flatten().Where(a => a.Item.RowId == agent->ResultItemId).ToList();
                if (gilShopItem is { Count: > 0 }) npcBuyPrice = item.Value.PriceMid;
            }

            for (var i = 0; i < 12 && i < component->ListLength; i++) {
                var listItem = component->ItemRendererList[i].AtkComponentListItemRenderer;

                var uldManager = listItem->AtkComponentButton.AtkComponentBase.UldManager;

                var singlePriceNode = (AtkTextNode*)uldManager.SearchNodeById(5);
                var qtyTextNode = (AtkTextNode*)uldManager.SearchNodeById(6);
                var totalTextNode = (AtkTextNode*)uldManager.SearchNodeById(8);
                var hqImageNode = (AtkImageNode*)uldManager.SearchNodeById(3);
                if (hqImageNode == null || totalTextNode == null || qtyTextNode == null || singlePriceNode == null) {
                    continue;
                }

                if (singlePriceNode->NodeText.StringPtr[0] == 0x20 || totalTextNode->NodeText.StringPtr[0] == 0x20) continue;

                var priceString = Common.ReadSeString(singlePriceNode->NodeText).TextValue.Replace($"{(char)SeIconChar.Gil}", "").Replace($",", "").Replace(" ", "").Replace($".", "");

                if (!ulong.TryParse(priceString, out var priceValue)) continue;
                if (!ushort.TryParse(Common.ReadSeString(qtyTextNode->NodeText).TextValue.Replace(",", "").Replace(".", ""), out var qtyValue)) continue;
                if (priceValue <= 0 || qtyValue <= 0) continue;

                var total = priceValue * qtyValue;

                var totalWithTax = total * 105 / 100;
                var realCostPerItem = totalWithTax / (float)qtyValue;

                var sellValue = Math.Ceiling(npcSellPrice * (hqImageNode->AtkResNode.IsVisible() ? 1.1 : 1.0));
                if (Config.HighlightLazyTax && npcBuyPrice > 0 && realCostPerItem > npcBuyPrice && !hqImageNode->AtkResNode.IsVisible()) {
                    singlePriceNode->EdgeColor.R = (byte)(Config.LazyTaxColour.X * 255f);
                    singlePriceNode->EdgeColor.G = (byte)(Config.LazyTaxColour.Y * 255f);
                    singlePriceNode->EdgeColor.B = (byte)(Config.LazyTaxColour.Z * 255f);
                    singlePriceNode->EdgeColor.A = (byte)(Config.LazyTaxColour.W * 255f);
                    singlePriceNode->TextFlags |= (byte)TextFlags.Edge;
                } else if (Config.HighlightNpcSellProfit && npcSellPrice > 0 && realCostPerItem < sellValue) {
                    singlePriceNode->EdgeColor.R = (byte)(Config.NpcSellProfitColour.X * 255f);
                    singlePriceNode->EdgeColor.G = (byte)(Config.NpcSellProfitColour.Y * 255f);
                    singlePriceNode->EdgeColor.B = (byte)(Config.NpcSellProfitColour.Z * 255f);
                    singlePriceNode->EdgeColor.A = (byte)(Config.NpcSellProfitColour.W * 255f);
                    singlePriceNode->TextFlags |= (byte)TextFlags.Edge;
                } else {
                    singlePriceNode->EdgeColor.R = 0;
                    singlePriceNode->EdgeColor.G = 0;
                    singlePriceNode->EdgeColor.B = 0;
                    singlePriceNode->EdgeColor.A = 0;
                }
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex, false, "Error in List Update");
        }
    }

    protected override void Disable() {
        if (Common.GetUnitBase("ItemSearchResult", out var unitBase)) unitBase->Close(true);
    }
}
