﻿using System;
using System.Threading;
using Dalamud;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class RefreshMarketPrices : Tweak
{
    public override string Name => "Refresh Market Prices";
    public override uint Version => 2;

    public override string Description =>
        "Retries to get prices upon receiving the 'Please wait and try your search again' message";

    protected override string Author => "Chalkos";

    private HookWrapper<HandlePrices> handlePricesHook;

    private delegate long HandlePrices(void* unk1, void* unk2, void* unk3, void* unk4, void* unk5, void* unk6,
        void* unk7);

    private IntPtr waitMessageCodeChangeAddress = IntPtr.Zero;
    private byte[] waitMessageCodeOriginalBytes = new byte[5];
    private bool waitMessageCodeErrored = false;

    private CancellationTokenSource cancelSource = null;

    protected override void Enable()
    {
        if (Enabled) return;

        handlePricesHook ??= Common.Hook<HandlePrices>("E8 ?? ?? ?? ?? 8B 5B 04 85 DB", HandlePricesDetour);
        handlePricesHook?.Enable();

        waitMessageCodeErrored = false;
        waitMessageCodeChangeAddress =
            Service.SigScanner.ScanText(
                "BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 8B C0 BA ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 45 33 C9");
        if (SafeMemory.ReadBytes(waitMessageCodeChangeAddress, 5, out waitMessageCodeOriginalBytes))
        {
            if (!SafeMemory.WriteBytes(waitMessageCodeChangeAddress, new byte[] { 0xBA, 0xB9, 0x1A, 0x00, 0x00 }))
            {
                waitMessageCodeErrored = true;
                SimpleLog.Error("Failed to write new instruction");
            }
        }
        else
        {
            waitMessageCodeErrored = true;
            SimpleLog.Error("Failed to read original instruction");
        }

        base.Enable();
    }

    private int failCount = 0;
    private int maxFailCount = 0;

    private long HandlePricesDetour(void* unk1, void* unk2, void* unk3, void* unk4, void* unk5, void* unk6, void* unk7)
    {
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        cancelSource = new CancellationTokenSource();

        var result = handlePricesHook.Original.Invoke(unk1, unk2, unk3, unk4, unk5, unk6, unk7);

        if (result != 1) {
            maxFailCount = Math.Max(++failCount, maxFailCount);
            Service.Framework.RunOnTick(() =>
            {
                if (Common.GetUnitBase<AddonItemSearchResult>(out var addonItemSearchResult)
                    && AddonItemSearchResultThrottled(addonItemSearchResult))
                {
                    Service.Framework.RunOnTick(RefreshPrices, TimeSpan.FromSeconds(2f + (0.5f * maxFailCount - 1)), 0, cancelSource.Token);
                }
            });
        } else {
            failCount = Math.Max(0, maxFailCount - 1);
        }

        return result;
    }

    private void RefreshPrices()
    {
        var addonItemSearchResult = Common.GetUnitBase<AddonItemSearchResult>();
        if (!AddonItemSearchResultThrottled(addonItemSearchResult)) return;
        Common.SendEvent(AgentId.ItemSearch, 2, 0, 0);
    }

    private bool AddonItemSearchResultThrottled(AddonItemSearchResult* addon) => addon != null
        && addon->ErrorMessage != null
        && addon->ErrorMessage->AtkResNode.IsVisible()
        && addon->HitsMessage != null
        && !addon->HitsMessage->AtkResNode.IsVisible();

    protected override void Disable()
    {
        if (!Enabled) return;
        if (!waitMessageCodeErrored && !SafeMemory.WriteBytes(waitMessageCodeChangeAddress, waitMessageCodeOriginalBytes))
        {
            SimpleLog.Error("Failed to write original instruction");
        }

        handlePricesHook?.Disable();
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        base.Disable();
    }

    public override void Dispose()
    {
        Disable();
        base.Dispose();
    }
}