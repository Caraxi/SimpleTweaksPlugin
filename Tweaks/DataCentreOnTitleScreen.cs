using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class DataCentreOnTitleScreen : Tweak {
    public override string Name => "Data Centre on Title Screen";
    public override string Description => "Shows the current Data Centre on the Title Screen";

    public override void Enable() {
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
        text->SetText(dc.Name.RawData.ToArray());
    }

    public override void Disable() {
        Service.Framework.Update -= FrameworkOnUpdate;
        base.Disable();
    }
}