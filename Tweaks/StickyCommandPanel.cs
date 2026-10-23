using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Sticky Command Panel")]
[TweakDescription("Automatically open/reopen command panel when it's closed.")]
[TweakAuthor("SubaruYashiro")]
[TweakAutoConfig]
public unsafe class StickyCommandPanel: UiAdjustments.SubTweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Auto (Re)Open While in Duty.")]
        public bool AutoOpenInDuty = true;
        [TweakConfigOption("Auto (Re)Open Anywhere.")]
        public bool AutoOpenEverywhere = true;
    }

    public Configs Config { get; private set; }

    protected override void Enable()
    {
        Service.Condition.ConditionChange += OnConditionChange;
    }

    protected override void Disable()
    {
        Service.Condition.ConditionChange -= OnConditionChange;
    }
    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (Service.Condition[ConditionFlag.BoundByDuty] && Config.AutoOpenInDuty || Config.AutoOpenEverywhere) CheckCommandPanelCondition();
    }

    private void CheckCommandPanelCondition() 
    {
        var unitBase = Common.GetUnitBase("QuickPanel");
        if (unitBase != null) return;
        AgentQuickPanel.Instance()->OpenPanel(AgentQuickPanel.Instance()->ActivePanel, false, false);
    }
}
