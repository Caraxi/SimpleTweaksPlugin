using Dalamud.Game;
using Dalamud.Game.Command;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin.TweakSystem;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class MainCommandCommand : Tweak {
    public override string Name => "Main Command Command";

    public override string Description => "Adds the command '/maincommand [name]' to allow using any Main Command from chat or macro.";

    public override void Enable() {
        Service.Commands.AddHandler("/maincommand", new CommandInfo(HandleCommand) { ShowInHelp = true, HelpMessage = "Execute a Main Command" });
        base.Enable();
    }

    private void HandleCommand(string command, string arguments) {

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

    public override void Disable() {
        Service.Commands.RemoveHandler("/maincommand");
        base.Disable();
    }

    public override void Dispose() {
        base.Dispose();
    }
}