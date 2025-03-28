using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("House Lights Command")]
[TweakDescription("Adds a command to control lighting in your own housing areas.")]
[TweakReleaseVersion("1.8.2.0")]
[Changelog("1.10.5.0", "Fixed tweak not working.")]
[Changelog(UnreleasedVersion, "Added ability to toggle SSAO with 'ssao-on' and 'ssao-off' parameters")]
public unsafe class HouseLightCommand : CommandTweak {
    public override bool Experimental => true;
    protected override string Command => "lights";
    protected override string HelpMessage => $"Adjust the lighting of the house or apartment you are currently in. /{CustomOrDefaultCommand} (0-5) [ssao-on | ssao-off] [save]";
    protected override bool ShowInHelp => true;

    private readonly string[] permanentMarkers = ["save"];

    protected override void OnCommand(string args) {
        var housingManager = HousingManager.Instance();

        if (!housingManager->IsInside()) {
            Service.Chat.PrintError("You must be inside a house or apartment to use that command.");
            return;
        }
        
        if (!housingManager->HasHousePermissions()) {
            Service.Chat.PrintError("You don't have permission to adjust the lights in this house/apartment.");
            return;
        }
        
        var s = args.Split(' ');
        if (s.Length < 1) {
            Service.Chat.PrintError($"/{CustomOrDefaultCommand} (0-5) [ssao-on | ssao-off] [save]");
            return;
        }

        var brightness = -1;
        var permanent = false;
        var ssaoEnable = housingManager->IndoorTerritory->SSAOEnable;
        
        foreach (var a in s) {
            if (byte.TryParse(a, out var o)) {
                if (o <= 5) brightness = o;
            }

            if (permanentMarkers.Contains(a, StringComparer.InvariantCultureIgnoreCase)) permanent = true;

            if (a.Equals("ssao-on", StringComparison.InvariantCultureIgnoreCase)) ssaoEnable = true;
            if (a.Equals("ssao-off", StringComparison.InvariantCultureIgnoreCase)) ssaoEnable = false;
        }

        if (brightness < 0) {
            Service.Chat.PrintError($"/{CustomOrDefaultCommand} (0-5) [ssao-on | ssao-off] [save]");
            return;
        }
        
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Housing);
        var isOpen = agent->IsAgentActive();
        
        Common.SendEvent(agent, 33, permanent ? 0 : 3, brightness, ssaoEnable);
        if (permanent) {
            // This just stops the housing menu from toggling
            if (!isOpen)
                agent->Hide();
            else
                agent->Show();
        } else {
            Service.Chat.Print(
                new SeString(
                    new TextPayload("Lighting has been changed on your client. Use "),
                    new UIForegroundPayload(500),
                    new TextPayload($"/{Command} {brightness}  {(ssaoEnable ? "ssao-on" : "ssao-off")} save"),
                    new UIForegroundPayload(0),
                    new TextPayload(" to make it permanent.")
                    )
                );
        }
    }
}
