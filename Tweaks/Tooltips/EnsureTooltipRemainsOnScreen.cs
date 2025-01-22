using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Ensure tooltips remain on screen")]
[TweakDescription("Prevents tooltips from extending below the bottom of the screen. Useful when using tweaks that make the tooltips longer.")]
[TweakCategory(TweakCategory.Tooltip, TweakCategory.UI)]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.6.0")]
public unsafe class EnsureTooltipRemainsOnScreen : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Padding", IntMin = 0, IntMax = 100, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 200)] public int Padding;
    }
    
    [TweakConfig] public Configs TweakConfig { get; protected set; }
    private readonly Dictionary<string, short> resetPositions = new();

    [AddonPostDraw("ItemDetail", "ActionDetail")]
    private void PostDraw(AtkUnitBase* unitBase) {
        if (!resetPositions.Remove(unitBase->NameString, out var value)) return;
        SimpleLog.Verbose($"Reset Position: {unitBase->NameString} to {value}");
        unitBase->RootNode->SetYShort(value);
    }

    [AddonPreUpdate("ItemDetail", "ActionDetail")]
    private void PreUpdate(AtkUnitBase* unitBase) {
        if (!unitBase->IsVisible) return;
        var screenHeight = Device.Instance()->Height - Math.Clamp(TweakConfig.Padding, 0, 100);
        var top = unitBase->RootNode->GetYShort();
        var window = unitBase->WindowNode;
        if (window == null) return;
        int height = window->GetHeight();
        var bottom = top + height;
        if (bottom <= screenHeight) return;
        var newY = (screenHeight - height);
        SimpleLog.Verbose($"Set Y Position: {unitBase->NameString} to {newY}");
        resetPositions[unitBase->NameString] = unitBase->RootNode->GetYShort();
        unitBase->RootNode->SetYShort((short)newY);
    }
}
