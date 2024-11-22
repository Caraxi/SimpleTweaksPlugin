using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Lumina.Text.ReadOnly;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Case Insensitive Text Commands")]
[TweakAuthor("KazWolfe")]
[TweakDescription("Allows text commands to be entered without caring about case.")]
[TweakAutoConfig]
public unsafe class CaseInsensitiveCommands : ChatTweaks.SubTweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Resolve Dalamud-provided commands)")]
        public bool ResolveDalamudCommands = true;
    }

    [TweakHook(typeof(ShellCommands), nameof(ShellCommands.TryInvokeDebugCommand), nameof(TryInvokeDebugCommandDetour))]
    private HookWrapper<ShellCommands.Delegates.TryInvokeDebugCommand> _tryInvokeCommandHook = null!;

    [TweakConfig] private Configs TweakConfig { get; set; } = null!;

    private int TryInvokeDebugCommandDetour(ShellCommands* self, Utf8String* message, UIModule* uiModule)
    {
        var retval = _tryInvokeCommandHook.Original(self, message, uiModule);
        if (retval != -1) return retval;

        var parsed = message->ToString().Split(" ", 2);

        if (FindInsensitiveGameCommand(parsed[0], out var matchedGameCommand))
        {
            // note: need to run this on next tick since we've already passed the point of command execution, but we're
            // still in the "on receive chat input" phase.
            Service.Framework.RunOnTick(() =>
            {
                ChatHelper.SendMessage(matchedGameCommand + (parsed.Length > 1 ? " " + parsed[1] : ""));
            });
            return 0;
        }

        if (TweakConfig.ResolveDalamudCommands && FindInsensitiveDalamudCommand(parsed[0], out var dalamudMatch))
        {
            Service.Commands.DispatchCommand(dalamudMatch.Key, parsed.Length > 1 ? parsed[1] : "", dalamudMatch.Value);
            return 0;
        }

        return retval;
    }

    private static bool FindInsensitiveDalamudCommand(string query, out KeyValuePair<string, IReadOnlyCommandInfo> result)
    {
        var match = Service.Commands.Commands
            .Where(tc => tc.Key.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            .FirstOrNull();

        if (match != null)
        {
            result = match.Value;
            return true;
        }

        result = default;
        return false;
    }

    private static bool FindInsensitiveGameCommand(string query, out string? result)
    {
        result = Service.Data.GetExcelSheet<TextCommand>()
            .Select(textCommand => (List<ReadOnlySeString>)
                [textCommand.Command, textCommand.ShortCommand, textCommand.Alias, textCommand.ShortAlias])
            .SelectMany(potentials => potentials
                .Select(potential => potential.ExtractText())
                .Where(extracted => extracted.Equals(query, StringComparison.InvariantCultureIgnoreCase)))
            .FirstOrDefault();

        return result != null;
    }
}