using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Command Panel Command")]
[TweakDescription("Adds a command to open specific panel")]
[TweakAuthor("SubaruYashiro")]
public unsafe class CommandPanelCommand : CommandTweak
{
    protected override string Command => "/cpanel";
    protected override string HelpMessage => "Open Command Panel Page (1-4)";

    protected override void OnCommand(string args)
    {
        var quickPanel = AgentQuickPanel.Instance();
        if (uint.TryParse(args, out uint value) && value >= 1 && value <= 4)
        {
            quickPanel->OpenPanel(value - 1, false, false);
        }
        else if (args == "")
        {
            quickPanel->OpenPanel(quickPanel->ActivePanel, false, false);
        }
    }
}
