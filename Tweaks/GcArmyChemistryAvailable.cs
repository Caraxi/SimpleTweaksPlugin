using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using GcArmyMember = FFXIVClientStructs.FFXIV.Client.Game.GcArmyMember;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Squadron Chemistry Available Icon")]
[TweakDescription("Show an icon over squadron members who have a new chemistry available.")]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class GcArmyChemistryAvailable : Tweak {
    protected override void AfterEnable() {
        TerritoryChanged(Service.ClientState.TerritoryType);
    }

    [TerritoryChanged]
    public void TerritoryChanged(ushort territoryId) {
        Common.FrameworkUpdate -= FrameworkUpdate;
        if (Service.Data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryType) && territoryType.TerritoryIntendedUse.RowId == 30) {
            Common.FrameworkUpdate += FrameworkUpdate;
        }
    }
    
    protected override void Disable() {
        Common.FrameworkUpdate -= FrameworkUpdate;
    }
    
    public void FrameworkUpdate() {
        if (Framework.Instance()->FrameCounter % 100 != 0) return;
        try {
            foreach (var c in Service.Objects) {
                if (c is not INpc { IsTargetable: true, ObjectKind: ObjectKind.EventNpc, NameId: > 0 } npc) continue;
                GcArmyMember* member = null;
                for (var i = 0U; i < GcArmyManager.Instance()->GetMemberCount(); i++) {
                    var m = GcArmyManager.Instance()->GetMember(i);
                    if (m == null || m->ENpcResidentId != npc.NameId) continue;
                    member = m;
                    break;
                }

                if (member == null) continue;
                var obj = (GameObject*)c.Address;
                obj->NamePlateIconId = member->InactiveTrait == 0 ? 0 : 60095U;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
}
