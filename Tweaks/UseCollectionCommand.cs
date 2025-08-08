using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Use Collection Command")]
[TweakDescription("Adds a command to use Collection items. /usecollection")]
[TweakCategory(TweakCategory.Command)]
[TweakAuthor("nebel")]
[TweakReleaseVersion("1.9.7.0")]
public unsafe class UseCollectionCommand : CommandTweak {
    protected override string HelpMessage => "Use a Collection item by name or ID. Use without parameters to list available items and IDs.";
    protected override string Command => "usecollection";

    private static readonly Dictionary<string, uint> McGuffin = Service.Data.GetExcelSheet<McGuffin>()!
        .Where(a => a.UIData.Value is { RowId: > 0 })
        .ToDictionary(b => b.UIData.Value.Name.ToString(), b => b.RowId, StringComparer.OrdinalIgnoreCase);

    protected override void OnCommand(string args) {
        var agent = AgentMcGuffin.Instance();

        if (args.Length == 0) {
            Service.Chat.Print("Available Collection items:");
            foreach (var (name, id) in McGuffin) {
                if (agent->CanOpenMcGuffin(id)) {
                    Service.Chat.Print($"  {name} (ID: {id})");
                }
            }

            return;
        }

        if (uint.TryParse(args, out var mcGuffinId) || McGuffin.TryGetValue(args, out mcGuffinId)) {
            if (McGuffin.ContainsValue(mcGuffinId) && agent->CanOpenMcGuffin(mcGuffinId)) {
                agent->OpenMcGuffin(mcGuffinId);
            }
        }
    }
}
