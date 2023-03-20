using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class ResizeExperienceBar : UiAdjustments.SubTweak
{
    public override string Name => "Change Size Experience Bar";
    public override string Description => "Changes the horizontal scale of the experience bar without affecting the text scale.";
    protected override string Author => "dlpoc";

    public class Configs : TweakConfig
    {
        [TweakConfigOption("Scale %", EditorSize = 140, IntMin = 0, IntMax = 100, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
        public int Scale = 100;

        [TweakConfigOption("Left align icons")]
        public bool LeftAlignIcons = false;
    }

    public Configs Config { get; private set; }
    public override bool UseAutoConfig => true;

    private HookWrapper<Common.AddonOnUpdate> onAddonUpdate;
    public override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
        onAddonUpdate ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 30 48 8B 72 18", AfterAddonUpdate);
        onAddonUpdate?.Enable();
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }
    private void AfterAddonUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        try
        {
            ResizeExpBar(Config.Scale);
            AlignImageNodes(Config.LeftAlignIcons);
        }
        catch (Exception ex)
        {
            Plugin.Error(this, ex);
        }
    }

    private void OnFrameworkUpdate()
    {
        ResizeExpBar(Config.Scale);
        AlignImageNodes(Config.LeftAlignIcons);
    }

    private void ResizeExpBar(int scale)
    {
        var unitBase = Common.GetUnitBase("_Exp");
        if (unitBase == null) return;
        var expBarNode = unitBase->GetNodeById(6);
        if (expBarNode == null) return;
        expBarNode->SetScaleX(scale / 100f);
    }

    private void AlignImageNodes(bool leftAlign)
    {
        // Original pos: (482,17) and (506,17)
        var unitBase = Common.GetUnitBase("_Exp");
        if (unitBase == null) return;

        var moonNode = unitBase->GetImageNodeById(3);
        if (moonNode == null) return;
        if (leftAlign)
        {
            moonNode->AtkResNode.SetPositionShort(-25, 17);
        }
        else
        {
            moonNode->AtkResNode.SetPositionShort(482, 17);
        }

        var daggerNode = unitBase->GetImageNodeById(2);
        if (daggerNode == null) return;
        if (leftAlign)
        {
            if (!moonNode->AtkResNode.IsVisible)
            {
                daggerNode->AtkResNode.SetPositionShort(-25, 17);
            }
            else
            {
                daggerNode->AtkResNode.SetPositionShort(-49, 17);
            }
        }
        else
        {
            if (!moonNode->AtkResNode.IsVisible)
            {
                daggerNode->AtkResNode.SetPositionShort(482, 17);
            }
            else
            {
                daggerNode->AtkResNode.SetPositionShort(506, 17);
            }
        }
    }

    public override void Disable()
    {
        onAddonUpdate?.Disable();
        ResizeExpBar(100);
        AlignImageNodes(false);
        SaveConfig(Config);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        base.Disable();
    }

    public override void Dispose()
    {
        onAddonUpdate?.Dispose();
        base.Dispose();
    }
}