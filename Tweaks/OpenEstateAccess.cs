using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility.Signatures;
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

    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 85 ?? ?? ?? ?? 40 32 ED E9 ?? ?? ?? ?? E8")]
    private readonly OpenEstateAccessSettingsDelegate openEstateAccessSettings = null!;

    private delegate HouseId GetOwnedHouseId(int type, int index = -1);

    [Signature("E8 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 8D 56 FE")]
    private readonly GetOwnedHouseId getOwnedHouseId = null!;

    [TweakHook(typeof(AtkUnitBase), nameof(AtkUnitBase.FireCallback), nameof(FireCallbackDetour), AutoEnable = false)]
    private readonly HookWrapper<AtkUnitBase.Delegates.FireCallback>? fireCallbackHook;

    [StructLayout(LayoutKind.Explicit, Size = 0xDE90)]
    private struct AgentHousing {
        [FieldOffset(0xA734)] public EstateType SelectedEstateType;
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
                if (getOwnedHouseId(0) == houseId) {
                    agent->SelectedEstateType = EstateType.FreeCompanyEstate;
                } else if (getOwnedHouseId(1) == houseId) {
                    agent->SelectedEstateType = EstateType.PersonalChambers;
                } else if (getOwnedHouseId(2) == houseId) {
                    agent->SelectedEstateType = EstateType.PersonalEstate;
                } else if (getOwnedHouseId(6) == houseId) {
                    agent->SelectedEstateType = EstateType.ApartmentRoom;
                } else {
                    Service.Chat.PrintError("You are not inside your own estate.", Name);
                    Service.Chat.PrintError(CommandUsage, Name);
                    return;
                }

                break;
            case "a":
            case "apartment":
                agent->SelectedEstateType = EstateType.ApartmentRoom;
                break;
            case "c":
            case "chambers":
                agent->SelectedEstateType = EstateType.PersonalChambers;
                break;
            case "p":
            case "personal":
                agent->SelectedEstateType = EstateType.PersonalEstate;
                break;
            case "f":
            case "fc":
            case "freecompany":
                agent->SelectedEstateType = EstateType.FreeCompanyEstate;
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
