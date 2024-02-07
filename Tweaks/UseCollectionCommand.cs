using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Use Collection Command")]
[TweakDescription("Adds a command to use Collection items. /usecollection")]
[TweakCategory(TweakCategory.Command)]
[TweakAuthor("nebel")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class UseCollectionCommand : CommandTweak {
    protected override string HelpMessage => "Use a Collection item by name or ID. Use without parameters to list available items and IDs.";
    protected override string Command => "usecollection";

    private static readonly Dictionary<string, uint> McGuffin = Service.Data.GetExcelSheet<McGuffinUIData>()!
        .Where(a => a.RowId > 0)
        .ToDictionary(b => b.Name.ToString().ToLower(), b => b.RowId);

    private delegate byte UseMcGuffinDelegate(IntPtr module, uint id);
    private UseMcGuffinDelegate? useMcGuffin;

    protected override void Enable() {
        if (useMcGuffin is null) {
            var useMcGuffinPtr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 0C 48 8B 07");
            useMcGuffin = Marshal.GetDelegateForFunctionPointer<UseMcGuffinDelegate>(useMcGuffinPtr);
        }
        base.Enable();
    }

    protected override void OnCommand(string args) {
        if (args.Length == 0) {
            var playerState = UIState.Instance()->PlayerState;
            Service.Chat.Print("Available Collection items:");
            foreach (var row in Service.Data.GetExcelSheet<McGuffinUIData>()!) {
                if (row.RowId > 0 && playerState.IsMcGuffinUnlocked(row.RowId)) {
                    Service.Chat.Print($"  {row.Name} (ID: {row.RowId})");
                }
            }

            return;
        }

        if (!uint.TryParse(args, out var mcGuffinId)) {
            if (!McGuffin.TryGetValue(args.ToLower(), out mcGuffinId)) {
                return;
            }
        }

        if (McGuffin.ContainsValue(mcGuffinId) && UIState.Instance()->PlayerState.IsMcGuffinUnlocked(mcGuffinId)) {
            var module =
                (IntPtr)Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.McGuffin);
            useMcGuffin?.Invoke(module, mcGuffinId);
        }
    }
}
