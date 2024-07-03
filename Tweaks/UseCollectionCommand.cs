using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
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
        .ToDictionary(b => b.UIData.Value.Name.ToString().ToLower(), b => b.RowId);

    private delegate byte UseMcGuffinDelegate(IntPtr module, uint id);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 40 80 3D ?? ?? ?? ?? ??")]
    private UseMcGuffinDelegate useMcGuffin = null!;

    protected override void OnCommand(string args) {
        if (args.Length == 0) {
            var playerState = UIState.Instance()->PlayerState;
            Service.Chat.Print("Available Collection items:");
            foreach (var row in Service.Data.GetExcelSheet<McGuffin>()!) {
                if (row.UIData.Value is { RowId: > 0 } uiData && playerState.IsMcGuffinUnlocked(row.RowId)) {
                    Service.Chat.Print($"  {uiData.Name} (ID: {row.RowId})");
                }
            }
            return;
        }

        if (!uint.TryParse(args, out var mcGuffinId) && !McGuffin.TryGetValue(args.ToLower(), out mcGuffinId)) return;
        if (McGuffin.ContainsValue(mcGuffinId) && UIState.Instance()->PlayerState.IsMcGuffinUnlocked(mcGuffinId)) {
            var module = (nint) AgentModule.Instance()->GetAgentByInternalId(AgentId.McGuffin);
            useMcGuffin(module, mcGuffinId);
        }
    }
}
