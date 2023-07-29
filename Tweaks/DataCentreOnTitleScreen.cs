using Dalamud.Game;
using Dalamud.Game.Config;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

[Changelog("1.8.9.0", "Added option to show the selected service account.")]
public unsafe class DataCentreOnTitleScreen : Tweak {
    public override string Name => "Data Centre on Title Screen";
    public override string Description => "Shows the current Data Centre on the Title Screen";

    public class Configs : TweakConfig {
        [TweakConfigOption("Show Service Account Number")]
        public bool ShowServiceAccountIndex;
    }
    
    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Framework.Update += FrameworkOnUpdate;
        base.Enable();
    }

    private void FrameworkOnUpdate(Framework framework) {
        if (Service.Condition.Any()) return;
        var addon = Common.GetUnitBase("_TitleMenu");
        if (addon == null) return;
        var dc = Service.Data.Excel.GetSheet<WorldDCGroupType>()?.GetRow(AgentLobby.Instance()->DataCenter);
        if (dc == null || dc.RowId == 0) {
            var world = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->SystemConfig.GetLastWorldID();
            dc = Service.Data.Excel.GetSheet<World>()?.GetRow(world)?.DataCenter?.Value;
        }
        if (dc == null) return;
        var button = (AtkComponentNode*) addon->GetNodeById(5);
        if (button == null || (ushort) button->AtkResNode.Type < 1000) return;
        var text = (AtkTextNode*) button->Component->UldManager.SearchNodeById(3);
        if (text == null || text->AtkResNode.Type != NodeType.Text) return;
        
        var displayText = $"{dc.Name.ToDalamudString().TextValue}";
        if (Config.ShowServiceAccountIndex) {
            var selectedServiceIndex = AgentLobby.Instance()->ServiceAccountIndex;
            if (selectedServiceIndex < 0) {
                if (Service.GameConfig.TryGet(SystemConfigOption.ServiceIndex, out uint lastServiceIndex)) {
                    selectedServiceIndex = (sbyte)lastServiceIndex;
                }
            }
            if (selectedServiceIndex >= 0) {
                displayText += $"\nService Account {selectedServiceIndex + 1}";
                text->TextFlags |= (byte)TextFlags.MultiLine;
                text->FontSize = 15;
                text->LineSpacing = 13;
                text->SetAlignment(AlignmentType.Top);
                text->SetText(displayText);
                return;
            }
        }
            
        text->FontSize = 18;
        text->LineSpacing = 18;
        unchecked { text->TextFlags &= (byte)~TextFlags.MultiLine; }
        text->SetAlignment(AlignmentType.Center);
        text->SetText(displayText);
    }

    protected override void Disable() {
        SaveConfig(Config);
        Service.Framework.Update -= FrameworkOnUpdate;
        base.Disable();
    }
}