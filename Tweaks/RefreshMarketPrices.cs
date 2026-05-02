using System;
using System.Threading;
using Dalamud;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Refresh Market Prices")]
[TweakDescription("Retries to get prices upon receiving the 'Please wait and try your search again' message")]
[TweakVersion(2)]
[TweakAuthor("Chalkos")]
public unsafe class RefreshMarketPrices : Tweak {
    [TweakHook(typeof(InfoProxyItemSearch), nameof(InfoProxyItemSearch.ProcessRequestResult), nameof(HandlePricesDetour))]
    private HookWrapper<InfoProxyItemSearch.Delegates.ProcessRequestResult> processRequestResultHook;

    [Signature("BA CE 07 00 00 E8 ?? ?? ?? ?? 4C 8B C0 BA ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 45 33 C9")]
    private readonly nint waitMessageCodeChangeAddress = IntPtr.Zero;

    private byte[] waitMessageCodeOriginalBytes = new byte[5];
    private bool waitMessageCodeErrored;

    private CancellationTokenSource cancelSource;

    protected override void Enable() {
        waitMessageCodeErrored = false;
        if (SafeMemory.ReadBytes(waitMessageCodeChangeAddress, 5, out waitMessageCodeOriginalBytes)) {
            if (!waitMessageCodeOriginalBytes.SequenceEqual(new byte[] { 0xBA, 0xCE, 0x07, 0x00, 0x00 })) throw new Exception("Unexpected original instruction.");
            if (!SafeMemory.WriteBytes(waitMessageCodeChangeAddress, [0xBA, 0xB9, 0x1A, 0x00, 0x00])) {
                waitMessageCodeErrored = true;
                SimpleLog.Error("Failed to write new instruction");
            }
        } else {
            waitMessageCodeErrored = true;
            SimpleLog.Error("Failed to read original instruction");
        }

        base.Enable();
    }

    private int failCount;
    private int maxFailCount;

    private void HandlePricesDetour(InfoProxyItemSearch* infoProxy, byte a2, int a3) {
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        cancelSource = new CancellationTokenSource();

        processRequestResultHook.Original.Invoke(infoProxy, a2, a3);

        maxFailCount = Math.Max(++failCount, maxFailCount);
        Service.Framework.RunOnTick(() => {
            if (Common.GetUnitBase<AddonItemSearchResult>(out var addonItemSearchResult) && AddonItemSearchResultThrottled(addonItemSearchResult)) {
                Service.Framework.RunOnTick(RefreshPrices, TimeSpan.FromSeconds(2f + (0.5f * maxFailCount - 1)), 0, cancelSource.Token);
            }
        });
    }

    private void RefreshPrices() {
        var addonItemSearchResult = Common.GetUnitBase<AddonItemSearchResult>();
        if (!AddonItemSearchResultThrottled(addonItemSearchResult)) return;
        Common.SendEvent(AgentId.ItemSearch, 2, 0, 0);
    }

    private bool AddonItemSearchResultThrottled(AddonItemSearchResult* addon) => addon != null
        && addon->ErrorMessage != null
        && addon->ErrorMessage->AtkResNode.IsVisible()
        && addon->HitsMessage != null
        && !addon->HitsMessage->AtkResNode.IsVisible();

    protected override void Disable() {
        if (!waitMessageCodeErrored && !SafeMemory.WriteBytes(waitMessageCodeChangeAddress, waitMessageCodeOriginalBytes)) {
            SimpleLog.Error("Failed to write original instruction");
        }

        cancelSource?.Cancel();
        cancelSource?.Dispose();
    }
}
