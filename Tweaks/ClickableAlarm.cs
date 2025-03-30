using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Clickable Alarm Icon")]
[TweakDescription("Allows clicking the alarm icon in the server bar to open the alarm window.")]
[TweakReleaseVersion(UnreleasedVersion)]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
public unsafe class ClickableAlarm : Tweak {
    private const uint AlarmImageNode = 6;

    private readonly SimpleEvent evt = new(EventAction);

    protected override void Enable() {
        if (Common.GetUnitBase("_DTR", out var addon)) SetupAddon(addon);
    }

    protected override void Disable() {
        if (Common.GetUnitBase("_DTR", out var addon)) CleanupAddon(addon);
        evt.Dispose();
    }

    [AddonPostSetup]
    public void SetupAddon(AtkUnitBase* unitBase) {
        CleanupAddon(unitBase);
        var node = unitBase->GetNodeById(AlarmImageNode);
        if (node == null) return;
        evt.Add(unitBase, node, AtkEventType.MouseClick);
    }

    [AddonFinalize]
    public void CleanupAddon(AtkUnitBase* unitBase) {
        var node = unitBase->GetNodeById(AlarmImageNode);
        if (node == null) return;
        evt.Remove(unitBase, node, AtkEventType.MouseClick);
    }

    private static void EventAction(AtkEventType eventType, AtkUnitBase* unitBase, AtkResNode* node) {
        if (eventType != AtkEventType.MouseClick) return;
        if (unitBase == null || unitBase->NameString != "_DTR") return;
        if (node == null || unitBase->GetNodeById(AlarmImageNode) != node) return;
        ChatHelper.SendMessage("/alarm");
    }
}
