using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class SetOptionCommand : CommandTweak {

    public override string Name => "Set Option Command";
    public override string Description => "Adds commands to change various settings.";
    protected override string Command => "setoption";
    protected override string HelpMessage => "Usage: /setoption <option> <value>";
    protected override string[] Alias => new[] { "setopt" };

    public override void Setup() {
        AddChangelog("1.8.3.2", "Improved reliability through patches");
        AddChangelog(Changelog.UnreleasedVersion, "Fixed issues when using gamepad mode");
        AddChangelog(Changelog.UnreleasedVersion, "Re-added accidentally remove gamepad mode option");
        AddChangelog(Changelog.UnreleasedVersion, "Added 'LimitMouseToGameWindow'");
        base.Setup();
    }

    public enum OptionGroup {
        System,
        UiConfig,
        UiControl,
    }

    public interface IOptionDefinition {
        public string Name { get; }
        public string ID { get; }
        public OptionGroup OptionGroup { get; }
        public string[] Alias { get; }
        
        public bool AllowToggle { get; }
        
        public IEnumerable<string> ValueNames { get; }
        public IEnumerable<string> AliasValueNames { get; }
    }
    
    public class OptionDefinition<T> : IOptionDefinition {
        public string Name { get; }
        public string ID { get; }
        public OptionGroup OptionGroup { get; }
        public string[] Alias { get; }
        
        public bool AllowToggle { get; set; }

        private readonly Lazy<(Dictionary<string, T> main, Dictionary<string, T> alias)> values;

        public Dictionary<string, T> Values => values.Value.main;
        public Dictionary<string, T> ValueAlias => values.Value.alias;
        public IEnumerable<string> ValueNames => Values.Keys;
        public IEnumerable<string> AliasValueNames => ValueAlias.Keys;
        public OptionDefinition(string name, string id, OptionGroup group, Func<(Dictionary<string, T>, Dictionary<string, T>)> valuesFunc, params string[] alias) {
            this.Name = name;
            this.ID = id;
            this.OptionGroup = group;
            this.Alias = alias;
            values = new Lazy<(Dictionary<string, T> main, Dictionary<string, T> alias)>(valuesFunc);
        }

    }

    private static class ValueType {
        public static (Dictionary<string, uint>, Dictionary<string, uint>) Boolean() {
            var main = new Dictionary<string, uint>() {
                ["off"] = 0, 
                ["on"] = 1
            };
            var alias = new Dictionary<string, uint>() {
                ["false"] = 0,
                ["0"] = 0,
                ["true"] = 1,
                ["1"] = 1,
            };
            return (main, alias);
        }

        public static (Dictionary<string, uint>, Dictionary<string, uint>) NamePlateDisplay() {
            var main = new Dictionary<string, uint> {
                ["always"] = 0,
                ["battle"] = 1,
                ["targeted"] = 2,
                ["never"] = 3,
            };
            var alias = new Dictionary<string, uint> {
                ["a"] = 0,
                ["b"] = 1,
                ["t"] = 2,
                ["target"] = 2,
                ["n"] = 3,
            };
            return (main, alias);
        }
    }
    
    private readonly List<IOptionDefinition> optionDefinitions = new() {
        new OptionDefinition<uint>("GamepadMode", "PadMode", OptionGroup.UiConfig, ValueType.Boolean, "gp") { AllowToggle = true },
        new OptionDefinition<uint>("ItemTooltips", "ItemDetailDisp", OptionGroup.UiControl, ValueType.Boolean, "itt") { AllowToggle = true },
        new OptionDefinition<uint>("ActionTooltips", "ActionDetailDisp", OptionGroup.UiControl, ValueType.Boolean, "att") { AllowToggle = true },
        new OptionDefinition<uint>("LegacyMovement", "MoveMode", OptionGroup.UiControl, ValueType.Boolean, "lm") { AllowToggle = true },
        new OptionDefinition<uint>("HideUnassignedHotbarSlots", "HotbarEmptyVisible", OptionGroup.UiConfig, ValueType.Boolean, "huhs") { AllowToggle = true },
        new OptionDefinition<uint>("LimitMouseToGameWindow", "MouseOpeLimit", OptionGroup.System, ValueType.Boolean, "lmtgw") { AllowToggle = true },

        new OptionDefinition<uint>("OwnDisplayName", "NamePlateDispTypeSelf", OptionGroup.UiConfig, ValueType.NamePlateDisplay, "odn") { AllowToggle = true },
        new OptionDefinition<uint>("PartyDisplayName", "NamePlateDispTypeParty", OptionGroup.UiConfig, ValueType.NamePlateDisplay, "pdn") { AllowToggle = true },
        new OptionDefinition<uint>("AllianceDisplayName", "NamePlateDispTypeAlliance", OptionGroup.UiConfig, ValueType.NamePlateDisplay, "adn") { AllowToggle = true },
        new OptionDefinition<uint>("OtherPlayerDisplayName", "NamePlateDispTypeOther", OptionGroup.UiConfig, ValueType.NamePlateDisplay, "opcdn") { AllowToggle = true },
        new OptionDefinition<uint>("FriendDisplayName", "NamePlateDispTypeFriend", OptionGroup.UiConfig, ValueType.NamePlateDisplay, "fdn") { AllowToggle = true },
        
        new OptionDefinition<uint>("DisplayNameSize", "NamePlateDispSize", OptionGroup.UiConfig, () => {
            return (
                new() { ["maximum"] = 2, ["large"] = 1, ["standard"] = 0 },
                new() { ["m"] = 2, ["max"] = 2, ["l"] = 1, ["s"] = 0, }
            );
        }, "dns") { AllowToggle = true },
    };

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
        ImGui.TextDisabled("/setopt list");
        ImGui.TextDisabled("/setopt [option] [value]");

        if (ImGui.TreeNode(LocString("Available Options") + "##optionListTree")) {
                    
            ImGui.Columns(3);
            ImGui.Text(LocString("option"));
            ImGui.NextColumn();
            ImGui.Text(LocString("values"));
            ImGui.NextColumn();
            ImGui.Text(LocString("alias"));
            ImGui.Separator();

            foreach (var o in optionDefinitions) {
                ImGui.NextColumn();
                ImGui.Text(o.Name);
                ImGui.NextColumn();
                var helpText = string.Join(" | ", o.ValueNames);
                if (o.AllowToggle) helpText += " | toggle";
                ImGui.Text(helpText);
                ImGui.NextColumn();
                var sb = new StringBuilder();
                foreach (var a in o.Alias) {
                    sb.Append(a);
                    sb.Append(' ');
                }
                ImGui.Text(sb.ToString());
            }

            ImGui.Columns();
            ImGui.TreePop();
        }
    };
    protected override void OnCommand(string arguments) {
        var configModule = ConfigModule.Instance();
        if (configModule == null) return;

        var argList = arguments.ToLower().Split(' ');

        if (argList[0] == "list") {

            var sb = new StringBuilder();

            foreach (var o in optionDefinitions) {
                sb.Append($"{o.Name} ");
            }
            Service.Chat.Print($"Options:\n{sb}");

            return;
        }
            
        var optionKind = argList[0];

        var optionDefinition =
            optionDefinitions.FirstOrDefault(o =>
                string.Equals(o.Name, optionKind, StringComparison.InvariantCultureIgnoreCase)) ??
            optionDefinitions.FirstOrDefault(o =>
                o.Alias.Any(a => string.Equals(a, optionKind, StringComparison.InvariantCultureIgnoreCase)));

        if (optionDefinition == null) {
            Service.Chat.PrintError("Unknown Option");
            Service.Chat.PrintError("/setoption list for a list of options");
            return;
        }

        var optionValue = "";
        if (argList.Length >= 2) {
            optionValue = argList[1];
        }

        var optionSection = optionDefinition.OptionGroup switch {
            OptionGroup.System => GameConfig.System,
            OptionGroup.UiConfig => GameConfig.UiConfig,
            OptionGroup.UiControl => GameConfig.UiControl,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var inputValue = optionValue.ToLowerInvariant();


        if (optionDefinition.AllowToggle && inputValue is "t" or "toggle") {

            switch (optionDefinition) {
                case OptionDefinition<uint> i: {
                    var optionDetail = optionSection[optionDefinition.ID];
                    if (optionDetail == null) {
                        Plugin.Error(this, new Exception($"Failed to get option detail for {optionDefinition.Name}"), allowContinue: true);
                        return;
                    }
                    
                    var toggleValue = optionSection.GetUInt(optionDefinition.ID);
                    toggleValue += 1;

                    if (toggleValue > optionDetail.Entry->Properties.UInt.MaxValue) {
                        toggleValue = optionDetail.Entry->Properties.UInt.MinValue;
                    }

                    optionSection.Set(optionDefinition.ID, toggleValue);

                    var valueName = i.Values.Where(kvp => kvp.Value == toggleValue).Select(kvp => kvp.Key).FirstOrDefault() ?? i.ValueAlias.Where(kvp => kvp.Value == toggleValue).Select(kvp => kvp.Key).FirstOrDefault() ?? $"{toggleValue}";
                    Service.Chat.Print($"{optionDefinition.Name} set to {valueName}");
                    return;
                }
            }
        }
        
        if (optionDefinition.ValueNames.Contains(inputValue)) {
            switch (optionDefinition) {
                case OptionDefinition<uint> i: {
                    optionSection.Set(optionDefinition.ID, i.Values[inputValue]);
                    break;
                }
                case OptionDefinition<float> f: {
                    optionSection.Set(optionDefinition.ID, f.Values[inputValue]);
                    break;
                }
            }
        } else if (optionDefinition.AliasValueNames.Contains(inputValue)) {
            switch (optionDefinition) {
                case OptionDefinition<uint> i: {
                    optionSection.Set(optionDefinition.ID, i.Values[inputValue]);
                    break;
                }
                case OptionDefinition<float> f: {
                    optionSection.Set(optionDefinition.ID, f.Values[inputValue]);
                    break;
                }
            }
        } else {
            Service.Chat.PrintError($"/setoption {optionKind} ({string.Join(" | ", optionDefinition.ValueNames)})");
        }
    }
}