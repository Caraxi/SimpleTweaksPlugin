using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Leave Duty Command")]
[TweakDescription("Adds a command to leave the currenty duty. /leaveduty")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class LeaveDutyCommand : CommandTweak {
    protected override string Command => "/leaveduty";
    protected override string HelpMessage => "Leave the current duty";

    private delegate byte CanLeaveCurrentDuty();

    [Signature("E8 ?? ?? ?? ?? 84 C0 74 18 48 8B 4F 10")]
    private CanLeaveCurrentDuty canLeaveCurrentDuty;
    
    protected override void OnCommand(string args) {
        if (Service.Condition.Cutscene()) {
            Service.Chat.PrintError("You cannot leave during a cutscene.");
            return;
        }

        if (!Service.Condition.Duty()) {
            Service.Chat.PrintError("You are not in a duty.");
            return;
            
        }
        
        if (canLeaveCurrentDuty() == 0) {
            Service.Chat.PrintError("You cannot leave your current duty.");
            return;
        }
        
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.ContentsFinderMenu);
        if (agent == null) return;
        Common.SendEvent(agent, 0, 0);
    }
}
