using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Main Command Command")]
[TweakDescription("Adds a command to allow using any Main Command from chat or macro.")]
public unsafe class MainCommandCommand : CommandTweak {
    protected override string Command => "maincommand";
    protected override string HelpMessage => "Execute a Main Command";

    protected override void OnCommand(string arguments) {
        if (uint.TryParse(arguments, out var id)) {
            Framework.Instance()->GetUIModule()->ExecuteMainCommand(id);
            return;
        }

        foreach (var mainCommand in Service.Data.Excel.GetSheet<MainCommand>()!) {
            if (arguments == mainCommand.Name.ToString()) {
                Framework.Instance()->GetUIModule()->ExecuteMainCommand(mainCommand.RowId);
                return;
            }
        }

        Service.Chat.PrintError($"'{arguments}' is not a valid 'Main Command'.");
    }
}
