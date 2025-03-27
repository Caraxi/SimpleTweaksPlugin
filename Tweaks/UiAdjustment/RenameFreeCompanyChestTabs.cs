using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Rename Free Company Chest Tabs")]
[TweakDescription("Allows renaming of each tab in the Free Company chest.")]
[TweakAuthor("croizat")]
[TweakAutoConfig(SaveOnChange = true)]
[TweakReleaseVersion("1.10.3.0")]
public unsafe class RenameFreeCompanyChestTabs : UiAdjustments.SubTweak {
    [TweakConfig] public Configuration Config { get; set; } = null!;

    public class Configuration : TweakConfig {
        public Dictionary<ulong, FreeCompanyConfig> FreeCompanies = new();

        public FreeCompanyConfig GetCompanyConfig() {
            var fcId = InfoProxyFreeCompany.Instance()->Id;
            return fcId == 0 ? null : FreeCompanies.GetValueOrDefault(fcId);
        }
    }

    public class FreeCompanyConfig {
        public string FreeCompanyName = string.Empty;
        public string[] TabNames = [];
    }

    protected void DrawConfig(ref bool hasChanged) {
        var fcConfig = Config.GetCompanyConfig();
        var fc = InfoProxyFreeCompany.Instance();
        if (fcConfig == null) {
            if (fc->Id == 0) {
                ImGui.TextDisabled("Free company data is not available.");
                return;
            }

            if (ImGui.Button($"Add config for '{Common.DefaultStringIfEmptyOrWhitespace(fc->NameString, "Free Company")}'")) {
                Config.FreeCompanies.Add(fc->Id, new FreeCompanyConfig { FreeCompanyName = Common.DefaultStringIfEmptyOrWhitespace(fc->NameString) });
                hasChanged = true;
            }

            return;
        }

        if (fcConfig.TabNames.Length < 5) fcConfig.TabNames = new string[5];

        ImGui.Text($"Config for {Common.DefaultStringIfEmptyOrWhitespace(fc->NameString, fcConfig.FreeCompanyName)}:");
        using (ImRaii.PushIndent()) {
            for (var i = 0; i < 5; i++) {
                if (fcConfig.TabNames[i] == null) fcConfig.TabNames[i] = string.Empty;
                hasChanged |= ImGui.InputText($"Tab {(char)(SeIconChar.BoxedNumber1 + i)}", ref fcConfig.TabNames[i], 20);
            }
        }
    }

    [AddonPreDraw("FreeCompanyChest")]
    private void AddonPreDraw(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase->RootNode == null) return;
        var fcConfig = Config.GetCompanyConfig();
        if (fcConfig == null) return;
        for (var i = 0; i < 5 && i < fcConfig.TabNames.Length; i++) {
            var node = Common.GetNodeByIDChain(atkUnitBase->RootNode, 1, 9, 10 + i, 9);
            var textNode = node != null ? node->GetAsAtkTextNode() : null;
            if (textNode is not null) textNode->NodeText.SetString(string.IsNullOrWhiteSpace(fcConfig.TabNames[i]) ? atkUnitBase->AtkValues[11 + i].ValueString() : $"{(char)(SeIconChar.BoxedNumber1 + i)} {fcConfig.TabNames[i]}");
        }
    }
}
