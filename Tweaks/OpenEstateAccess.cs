using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Estate Access Command")]
[TweakDescription("Adds a command to open the estate access configuration for the current estate.")]
[TweakReleaseVersion("1.10.5.0")]
public unsafe class OpenEstateAccess : CommandTweak {
    private delegate byte OpenEstateAccessSettingsDelegate(AgentHousing* agentHousing);

    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 85 ?? ?? ?? ?? 40 32 F6 E9 ?? ?? ?? ?? E8")]
    private readonly OpenEstateAccessSettingsDelegate openEstateAccessSettings = null!;

    [TweakHook(typeof(AtkUnitBase), nameof(AtkUnitBase.FireCallback), nameof(FireCallbackDetour), AutoEnable = false)]
    private readonly HookWrapper<AtkUnitBase.Delegates.FireCallback>? fireCallbackHook;

    [StructLayout(LayoutKind.Explicit, Size = 0xDE90)]
    [Agent(AgentId.Housing)]
    private struct AgentHousing {
        [FieldOffset(0xA734)] public SelectedEstateType SelectedEstateType;
    }

    private enum SelectedEstateType {
        // We'll never know why SE made this order different.
        PersonalEstate,
        FreeCompanyEstate,
        PersonalChambers,
        ApartmentRoom,
    }
    
    private SeString CommandUsage {
        get {
            var s = new SeStringBuilder();
            s.AddText($"/{CustomOrDefaultCommand} [");
            var f = true;
            foreach (var a in new[] { "apartment", "chambers", "personal", "fc" }) {
                if (!f) s.AddText("|");
                f = false;
                s.AddUiForeground(a, 34);
            }

            s.AddText("]");
            return s.Build();
        }
    }


    protected void DrawConfig() {
        
    }
    
    public void FireCallbackDetour(AtkUnitBase* atkUnitBase, uint valueCount, AtkValue* values, bool close) {
        fireCallbackHook?.Original(atkUnitBase, valueCount, values, close);
        try {
            if (atkUnitBase->NameString != "HousingConfig") return;
            fireCallbackHook?.Disable();
            AgentModule.Instance()->GetAgentByInternalId(AgentId.Housing)->Hide();
        } catch {
            fireCallbackHook?.Disable();
        }
    }

    protected override void OnCommand(string args) {
        var agent = (AgentHousing*)AgentModule.Instance()->GetAgentByInternalId(AgentId.Housing);
        switch (args.ToLower()
                    .Trim()) {
            case "":
                var housingManager = HousingManager.Instance();
                if (housingManager == null || housingManager->IsInside() == false) {
                    Service.Chat.PrintError("You are not inside your own estate.", Name);
                    Service.Chat.PrintError(CommandUsage, Name);
                    return;
                }

                var houseId = housingManager->GetCurrentIndoorHouseId();
                if (HousingManager.GetOwnedHouseId(EstateType.FreeCompanyEstate) == houseId) {
                    agent->SelectedEstateType = SelectedEstateType.FreeCompanyEstate;
                } else if (HousingManager.GetOwnedHouseId(EstateType.PersonalChambers) == houseId) {
                    agent->SelectedEstateType = SelectedEstateType.PersonalChambers;
                } else if (HousingManager.GetOwnedHouseId(EstateType.PersonalEstate) == houseId) {
                    agent->SelectedEstateType = SelectedEstateType.PersonalEstate;
                } else if (HousingManager.GetOwnedHouseId(EstateType.ApartmentRoom) == houseId) {
                    agent->SelectedEstateType = SelectedEstateType.ApartmentRoom;
                } else {
                    Service.Chat.PrintError("You are not inside your own estate.", Name);
                    Service.Chat.PrintError(CommandUsage, Name);
                    return;
                }

                break;
            case "a":
            case "apartment":
                agent->SelectedEstateType = SelectedEstateType.ApartmentRoom;
                break;
            case "c":
            case "chambers":
                agent->SelectedEstateType = SelectedEstateType.PersonalChambers;
                break;
            case "p":
            case "personal":
                agent->SelectedEstateType = SelectedEstateType.PersonalEstate;
                break;
            case "f":
            case "fc":
            case "freecompany":
                agent->SelectedEstateType = SelectedEstateType.FreeCompanyEstate;
                break;
            default:
                Service.Chat.PrintError("Unrecognised Estate Type", Name);
                Service.Chat.PrintError(CommandUsage, Name);
                return;
        }

        fireCallbackHook?.Enable();
        openEstateAccessSettings(agent);
    }

    protected override string Command => "/estateaccess";
}
