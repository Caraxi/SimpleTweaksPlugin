using System;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Game;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class RefreshMarketPrices : Tweak
{
    public override string Name => "Refresh Market Prices";
    public override string Description => "Retries to get prices upon receiving the 'Please wait and try your search again' message";
    protected override string Author => "Chalkos";

    private HookWrapper<HandlePrices> handlePricesHook;

    private delegate void* HandlePrices(void* unk1, void* unk2, void* unk3, void* unk4, void* unk5, void* unk6,
        void* unk7);

    private HookWrapper<Callback> callbackHook;

    private delegate void* Callback(AtkUnitBase* atkUnitBase, int count, AtkValue* values, void* a4);

    private CancellationTokenSource cancelSource = null;

    private int itemSearchLastSelectedItem = 0;


    public override void Enable()
    {
        handlePricesHook ??= Common.Hook<HandlePrices>("E8 ?? ?? ?? ?? 8B 5B 04 85 DB", HandlePricesDetour);
        handlePricesHook?.Enable();

        callbackHook ??= Common.Hook<Callback>("E8 ?? ?? ?? ?? 8B 4C 24 20 0F B6 D8", CallbackDetour);
        callbackHook?.Enable();
        base.Enable();
    }

    private void* CallbackDetour(AtkUnitBase* atkUnitBase, int count, AtkValue* values, void* a4)
    {
        if (atkUnitBase != Common.GetUnitBase("ItemSearch")) goto Original;
        if (count != 2) goto Original;
        if (values->Type != ValueType.Int || values->Int != 5) goto Original;
        var value2 = values + 1;
        if (value2->Type != ValueType.Int) goto Original;

        itemSearchLastSelectedItem = value2->Int;

        Original:
        return callbackHook.Original(atkUnitBase, count, values, a4);
    }

    private void* HandlePricesDetour(void* unk1, void* unk2, void* unk3, void* unk4, void* unk5, void* unk6, void* unk7)
    {
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        cancelSource = new CancellationTokenSource();

        Service.Framework.RunOnTick(() =>
        {
            if (Common.GetUnitBase<AddonItemSearchResult>(out var addonItemSearchResult)
                && AddonItemSearchResultThrottled(addonItemSearchResult))
            {
                Service.Framework.RunOnTick(RefreshPrices, TimeSpan.FromSeconds(0.5), 0, cancelSource.Token);
            }
        });

        return handlePricesHook.Original.Invoke(unk1, unk2, unk3, unk4, unk5, unk6, unk7);
    }

    private void RefreshPrices()
    {
        var addonItemSearchResult = Common.GetUnitBase<AddonItemSearchResult>();
        var addonItemSearch = Common.GetUnitBase("ItemSearch");
        var addonRetainerSell = Common.GetUnitBase("RetainerSell");

        if (!AddonItemSearchResultThrottled(addonItemSearchResult) ||
            ((addonItemSearch == null || !addonItemSearch->IsVisible) &&
             (addonRetainerSell == null || !addonRetainerSell->IsVisible))) return;

        Common.GenerateCallback(&addonItemSearchResult->AtkUnitBase, -1);

        if (addonItemSearch != null && addonItemSearch->IsVisible)
        {
            Common.GenerateCallback(addonItemSearch, 5, itemSearchLastSelectedItem);
        }
        else if (addonRetainerSell != null && addonRetainerSell->IsVisible)
        {
            Common.GenerateCallback(addonRetainerSell, 4);
        }
    }

    private bool AddonItemSearchResultThrottled(AddonItemSearchResult* addon) => addon != null
        && addon->ErrorMessage != null
        && addon->ErrorMessage->AtkResNode.IsVisible
        && addon->HitsMessage != null
        && !addon->HitsMessage->AtkResNode.IsVisible;

    public override void Disable()
    {
        callbackHook?.Disable();
        handlePricesHook?.Disable();
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        base.Disable();
    }

    public override void Dispose()
    {
        callbackHook?.Dispose();
        handlePricesHook?.Dispose();
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        base.Dispose();
    }
}