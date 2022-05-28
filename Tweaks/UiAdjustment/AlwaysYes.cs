using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class AlwaysYes : UiAdjustments.SubTweak {
    public override string Name => "Always Yes";
    public override string Description => "Default cursor to yes when using confirm (num 0).";
    protected override string Author => "Aireil";
    
    public class Configs : TweakConfig {
        public bool YesNo = true;
        public bool DutyConfirmation = true;
        public bool CardsShop = true;
        public bool RetainerVentures = true;
        public bool MateriaMelds = true;
        public bool MateriaExtractions = true;
        public bool MateriaRetrievals = true;
        public bool GlamourDispels = true;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("Enable for:");
        hasChanged |= ImGui.Checkbox("Most yes/(hold)/no dialogs", ref Config.YesNo);
        hasChanged |= ImGui.Checkbox("Duty confirmations", ref Config.DutyConfirmation);
        hasChanged |= ImGui.Checkbox("TT cards sales", ref Config.CardsShop);
        hasChanged |= ImGui.Checkbox("Retainer ventures", ref Config.RetainerVentures);
        hasChanged |= ImGui.Checkbox("Materia melds", ref Config.MateriaMelds);
        hasChanged |= ImGui.Checkbox("Materia extractions", ref Config.MateriaExtractions);
        hasChanged |= ImGui.Checkbox("Materia retrievals", ref Config.MateriaRetrievals);
        hasChanged |= ImGui.Checkbox("Glamour dispels", ref Config.GlamourDispels);

        if (hasChanged) {
            SaveConfig(Config);
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Common.AddonSetup += OnAddonSetup;
        base.Enable();
    }

    private void OnAddonSetup(SetupAddonArgs args) {
        switch (args.AddonName) {
            case "SelectYesno": 
                if (Config.YesNo) SetFocusYes(args.Addon, 8, 9); 
                return;
            case "ContentsFinderConfirm": 
                if (Config.DutyConfirmation) SetFocusYes(args.Addon, 63);
                return;
            case "ShopCardDialog": 
                if (Config.CardsShop) SetFocusYes(args.Addon, 16); 
                return;
            case "RetainerTaskAsk": 
                if (Config.RetainerVentures) SetFocusYes(args.Addon, 40); 
                return;
            case "RetainerTaskResult": 
                if (Config.RetainerVentures) SetFocusYes(args.Addon, 20); 
                return;
            case "MateriaAttachDialog":
                if (Config.MateriaMelds) SetFocusYes(args.Addon, 35);
                return;
            case "MaterializeDialog":
                if (Config.MateriaExtractions) SetFocusYes(args.Addon, 13);
                return;
            case "MateriaRetrieveDialog":
                if (Config.MateriaRetrievals) SetFocusYes(args.Addon, 17);
                return;
            case "MiragePrismRemove":
                if (Config.GlamourDispels) SetFocusYes(args.Addon, 15);
                return;
        }
    }
    
    private static void SetFocusYes(AtkUnitBase* unitBase, uint yesButtonId, uint? yesHoldButtonId = null) {
        var yesButton = unitBase->UldManager.SearchNodeById(yesButtonId);
        if (yesButton == null) return;

        var isYesHoldVersion = yesHoldButtonId != null && !yesButton->IsVisible;
        if (isYesHoldVersion) {
            yesButton = unitBase->UldManager.SearchNodeById(yesHoldButtonId.Value);
            if (yesButton == null) return;
        }

        var yesCollision = ((AtkComponentNode *)yesButton)->Component->UldManager.SearchNodeById(isYesHoldVersion ? 7u : 4u);
        if (yesCollision == null) return;

        unitBase->SetFocusNode(yesCollision);
        unitBase->CursorTarget = yesCollision;
    }

    public override void Disable() {
        Common.AddonSetup -= OnAddonSetup;
        base.Disable();
    }
}
