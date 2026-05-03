using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Auto Level Sync")]
[TweakDescription("Automaticaly sync your level during fates when needed.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.QoL)]
public unsafe class AutoLevelSync : Tweak
{
    private bool wasInFate;

    protected override void Enable()
    {
        Service.Framework.Update += OnUpdate;
    }

    protected override void Disable()
    {
        Service.Framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        try
        {
            var fateManager = FateManager.Instance();
            if (fateManager == null)
                return;

            var fate = fateManager->CurrentFate;
            bool inFate = fate != null;

            if (inFate && !wasInFate)
            {
                TrySync();
            }

            wasInFate = inFate;
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "AutoLevelSync error");
        }
    }

private void TrySync()
{
    try
    {
        ChatHelper.SendMessage("/levelsync");
    }
    catch (Exception ex)
    {
        SimpleLog.Error(ex, "AutoLevelSync TrySync error");
    }
}
}