using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using SimpleTweaksPlugin.Events;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Change Size Experience Bar")]
[TweakDescription("Changes the horizontal scale of the experience bar without affecting the text scale.")]
[TweakAuthor("dlpoc")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.0.0")]
public unsafe class ResizeExperienceBar : UiAdjustments.SubTweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Scale %", EditorSize = 140, IntMin = 0, IntMax = 100, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
        public int Scale = 100;

        [TweakConfigOption("Left align icons")]
        public bool LeftAlignIcons;
    }

    public Configs Config { get; private set; }

    protected override void Enable() => UpdateAddon();
    protected override void Disable() => RevertAddon();
    protected override void ConfigChanged() => UpdateAddon();

    [AddonPostRequestedUpdate("_Exp")]
    private void AfterAddonUpdate() => UpdateAddon();

    private void RevertAddon()
    {
        ResizeExpBar(100);
        AlignImageNodes(false);
    }

    private void UpdateAddon()
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
            moonNode->SetPositionShort(-25, 17);
        }
        else
        {
            moonNode->SetPositionShort(482, 17);
        }

        var daggerNode = unitBase->GetImageNodeById(2);
        if (daggerNode == null) return;
        if (leftAlign)
        {
            if (!moonNode->IsVisible())
            {
                daggerNode->SetPositionShort(-25, 17);
            }
            else
            {
                daggerNode->SetPositionShort(-49, 17);
            }
        }
        else
        {
            if (!moonNode->IsVisible())
            {
                daggerNode->SetPositionShort(482, 17);
            }
            else
            {
                daggerNode->SetPositionShort(506, 17);
            }
        }
    }
}