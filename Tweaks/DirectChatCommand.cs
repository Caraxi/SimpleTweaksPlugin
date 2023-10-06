using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks;

public class DirectChatCommand : CommandTweak
{
    protected override string Author => "SayuShira";
    public override string Name => "Direct Chat Command";
    protected override string Command => "directchat";
    public override string Description => "Adds a command to toggle direct chat mode on and off.";
    protected override string HelpMessage => "Toggles the game setting for direct chat";

    protected override void OnCommand(string arguments)
    {
        Service.GameConfig.UiControl.TryGet("DirectChat", out bool directChat);
        // PluginLog.Debug($"Is Direct Chat on? {directChat.ToString()}");

        Service.GameConfig.UiControl.Set("DirectChat", !directChat);

        // The initial bool is always the opposite value of what will be used
        var status = !directChat ? "active" : "disabled";

        Service.Chat.Print($"DirectChat is now {status}.");
    }
}
