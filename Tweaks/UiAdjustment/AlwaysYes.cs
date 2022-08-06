using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface;
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
        public bool Desynthesis = true;
        public bool Lobby = true;
        public List<string> ExceptionsYesNo = new();
    }

    public Configs Config { get; private set; }

    private string newException = string.Empty;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("Enable for:");
        hasChanged |= ImGui.Checkbox("Most yes/(hold)/no dialogs", ref Config.YesNo);

        ImGui.Indent();
        if (ImGui.CollapsingHeader("Exceptions##AlwaysYes")) {
            ImGui.Text("Do not change default if dialog text contains:");
            for (var  i = 0; i < Config.ExceptionsYesNo.Count; i++) {
                ImGui.PushID($"AlwaysYesBlacklist_{i.ToString()}");
                var exception = Config.ExceptionsYesNo[i];
                if (ImGui.InputText("##AlwaysYesTextBlacklist", ref exception, 500)) {
                    Config.ExceptionsYesNo[i] = exception;
                    hasChanged = true;
                }

                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString())) {
                    Config.ExceptionsYesNo.RemoveAt(i--);
                    hasChanged = true;
                }

                ImGui.PopFont();
                ImGui.PopID();
                if (i < 0) break;
            }

            ImGui.InputText("##AlwaysYesNewTextException", ref newException, 500);
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString())) {
                if (newException != string.Empty) {
                    Config.ExceptionsYesNo.Add(newException);
                    newException = string.Empty;
                    hasChanged = true;
                }
            }

            ImGui.PopFont();
        }

        ImGui.Unindent();
        hasChanged |= ImGui.Checkbox("Duty confirmations", ref Config.DutyConfirmation);
        hasChanged |= ImGui.Checkbox("TT cards sales", ref Config.CardsShop);
        hasChanged |= ImGui.Checkbox("Retainer ventures", ref Config.RetainerVentures);
        hasChanged |= ImGui.Checkbox("Materia melds", ref Config.MateriaMelds);
        hasChanged |= ImGui.Checkbox("Materia extractions", ref Config.MateriaExtractions);
        hasChanged |= ImGui.Checkbox("Materia retrievals", ref Config.MateriaRetrievals);
        hasChanged |= ImGui.Checkbox("Glamour dispels", ref Config.GlamourDispels);
        hasChanged |= ImGui.Checkbox("Desynthesis", ref Config.Desynthesis);
        hasChanged |= ImGui.Checkbox("Character selection dialogs", ref Config.Lobby);

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
                if (Config.YesNo && !IsYesnoAnException(args.Addon)) SetFocusYes(args.Addon, 8, 9);
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
            case "SalvageDialog":
                if (Config.Desynthesis) SetFocusYes(args.Addon, 24);
                return;
            case "LobbyWKTCheck":
                if (Config.Lobby) SetFocusYes(args.Addon, 4);
                return;
            case "LobbyDKTWorldList":
                if (Config.Lobby) SetFocusYes(args.Addon, 23);
                return;
            case "LobbyDKTCheckExec":
                if (Config.Lobby) SetFocusYes(args.Addon, 3);
                return;
        }
    }

    private static void SetFocusYes(AtkUnitBase* unitBase, uint yesButtonId, uint? yesHoldButtonId = null) {
        if (unitBase == null) return;

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

    private bool IsYesnoAnException(AtkUnitBase* unitBase) {
        if (Config.ExceptionsYesNo.Count == 0 || unitBase == null) return false;

        var textNode = (AtkTextNode *)unitBase->UldManager.SearchNodeById(2);
        if (textNode == null) return false;

        var text = Common.ReadSeString(textNode->NodeText).TextValue;

        return text != string.Empty && Config.ExceptionsYesNo.Any(val => text.Contains(val));
    }

    public override void Disable() {
        Common.AddonSetup -= OnAddonSetup;
        base.Disable();
    }
}
