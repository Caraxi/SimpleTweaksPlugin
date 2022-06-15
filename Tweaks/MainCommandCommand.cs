using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class MainCommandCommand : CommandTweak {
    public override string Name => "Main Command Command";
    public override string Description => $"Adds the command '/{Command} [name]' to allow using any Main Command from chat or macro.";
    protected override string Command => "maincommand";
    protected override string HelpMessage => "Execute a Main Command";

    protected override void OnCommand(string arguments) {
        if (uint.TryParse(arguments, out var id)) {
            Framework.Instance()->GetUiModule()->ExecuteMainCommand(id);
            return;
        }

        foreach (var mainCommand in Service.Data.Excel.GetSheet<MainCommand>()) {
            if (arguments == mainCommand.Name.ToString()) {
                Framework.Instance()->GetUiModule()->ExecuteMainCommand(mainCommand.RowId);
                return;
            }
        }

        Service.Chat.PrintError($"'{arguments}' is not a valid 'Main Command'.");
    }
}