using Dalamud.Game.Config;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[Changelog("1.8.9.0", "Added option to show the selected service account.")]
[TweakCategory(TweakCategory.UI)]
[TweakName("Data Centre on Title Screen")]
[TweakDescription("Shows the current Data Centre on the Title Screen")]
[TweakAutoConfig]
public unsafe class DataCentreOnTitleScreen : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Show Service Account Number")]
        public bool ShowServiceAccountIndex;
    }

    [TweakConfig] public Configs Config { get; private set; }

    [FrameworkUpdate]
    private void FrameworkOnUpdate() {
        if (Service.Condition.Any()) return;
        var addon = Common.GetUnitBase("_TitleMenu");
        if (addon == null) return;
        if (!Service.Data.Excel.GetSheet<WorldDCGroupType>().TryGetRow(AgentLobby.Instance()->DataCenter, out var dc) || dc.RowId == 0) {
            var world = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->SystemConfig.GetLastWorldId();
            if (Service.Data.Excel.GetSheet<World>().TryGetRow(world, out var worldRow)) {
                dc = worldRow.DataCenter.Value;
            } else {
                return;
            }
        }
        
        var button = (AtkComponentNode*)addon->GetNodeById(5);
        if (button == null || (ushort)button->AtkResNode.Type < 1000) return;
        var text = (AtkTextNode*)button->Component->UldManager.SearchNodeById(3);
        if (text == null || text->AtkResNode.Type != NodeType.Text) return;

        var displayText = $"{dc.Name.ExtractText()}";
        if (Config.ShowServiceAccountIndex) {
            var selectedServiceIndex = AgentLobby.Instance()->ServiceAccountIndex;
            if (selectedServiceIndex < 0) {
                if (Service.GameConfig.TryGet(SystemConfigOption.ServiceIndex, out uint lastServiceIndex)) {
                    selectedServiceIndex = (sbyte)lastServiceIndex;
                }
            }

            if (selectedServiceIndex >= 0) {
                displayText += $"\nService Account {selectedServiceIndex + 1}";
                text->TextFlags |= TextFlags.MultiLine;
                text->FontSize = 15;
                text->LineSpacing = 13;
                text->SetAlignment(AlignmentType.Top);
                text->SetText(displayText);
                return;
            }
        }

        text->FontSize = 18;
        text->LineSpacing = 18;
        text->TextFlags &= ~TextFlags.MultiLine;

        text->SetAlignment(AlignmentType.Center);
        text->SetText(displayText);
    }
}
