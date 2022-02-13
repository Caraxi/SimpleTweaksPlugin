using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Command;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class SetOptionCommand : Tweak {

    public override string Name => "Set Option Command";
    public override string Description => "Adds commands to change various settings.";

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)] 
    private delegate void SetGamepadMode(ConfigModule* configModule, ulong value);

    private SetGamepadMode setGamepadMode;

    public enum OptionType {
        Bool,
        ToggleGamepadMode, // bool with extra shit
        NameDisplayModeBattle,
        NameDisplayMode,
    }

    public class OptionDefinition {
        public string Name { get; }
        public short ID { get; }
        public OptionType OptionType { get; }
        public string[] Alias { get; }

        public OptionDefinition(string name, short id, OptionType type, params string[] alias) {
            this.Name = name;
            this.ID = id;
            this.OptionType = type;
            this.Alias = alias;
        }

    }

    private readonly List<OptionDefinition> optionDefinitions = new() {
        new OptionDefinition("GamepadMode", 89, OptionType.ToggleGamepadMode, "gp"),

        new OptionDefinition("ItemTooltips", 716, OptionType.Bool, "itt"),
        new OptionDefinition("ActionTooltips", 721, OptionType.Bool, "att"),
        new OptionDefinition("LegacyMovement", 304, OptionType.Bool, "lm"),

        new OptionDefinition("OwnDisplayName", 443, OptionType.NameDisplayModeBattle, "odn"),
        new OptionDefinition("PartyDisplayName", 456, OptionType.NameDisplayModeBattle, "pdn"),
        new OptionDefinition("AllianceDisplayName", 465, OptionType.NameDisplayModeBattle, "adn"),
        new OptionDefinition("OtherPlayerDisplayName", 472, OptionType.NameDisplayModeBattle, "opcdn"),
        new OptionDefinition("FriendDisplayName", 517, OptionType.NameDisplayModeBattle, "fdn"),
    };


    private readonly Dictionary<OptionType, string> optionTypeValueHints = new Dictionary<OptionType, string> {
        {OptionType.Bool, "on | off | toggle"},
        {OptionType.ToggleGamepadMode, "on | off | toggle"},
        {OptionType.NameDisplayModeBattle, "always | battle | targeted | never"},
    };
        
    public override void Setup() {
        if (Ready) return;

        try {
            var toggleGamepadModeAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 40 0F B6 DF 49 8B CC");
            SimpleLog.Verbose($"ToggleGamePadModeAddress: {toggleGamepadModeAddress.ToInt64():X}");
            setGamepadMode = Marshal.GetDelegateForFunctionPointer<SetGamepadMode>(toggleGamepadModeAddress);

            Ready = true;

        } catch (Exception ex) {
            SimpleLog.Error($"Failed to setup {this.GetType().Name}: {ex.Message}");
        }
    }

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
                if (o.OptionType == OptionType.ToggleGamepadMode) continue;
                ImGui.NextColumn();
                ImGui.Text(o.Name);
                ImGui.NextColumn();
                ImGui.Text(optionTypeValueHints.ContainsKey(o.OptionType) ? optionTypeValueHints[o.OptionType] : "");
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

    public override void Enable() {
        if (!Ready) return;

        Service.Commands.AddHandler("/setoption", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = true});
        Service.Commands.AddHandler("/setopt", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = false});

        Enabled = true;
    }

    private void OptionCommand(string command, string arguments) {
        var configModule = ConfigModule.Instance();
        if (configModule == null) return;

        var argList = arguments.ToLower().Split(' ');

        if (argList[0] == "list") {

            var sb = new StringBuilder();

            foreach (var o in optionDefinitions) {
                if (o.OptionType == OptionType.ToggleGamepadMode) continue;
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

        var setValue = 0UL;
        switch (optionDefinition.OptionType) {
                
            case OptionType.ToggleGamepadMode:
            case OptionType.Bool: {

                switch (optionValue) {
                    case "1":
                    case "true":
                    case "on":
                        configModule->SetOptionById(optionDefinition.ID, 1);
                        setValue = 1;
                        break;
                    case "0":
                    case "false":
                    case "off":
                        configModule->SetOptionById(optionDefinition.ID, 0);
                        setValue = 0;
                        break;
                    case "":
                    case "t":
                    case "toggle":
                        var cVal = configModule->GetIntValue(optionDefinition.ID);
                        configModule->SetOptionById(optionDefinition.ID, cVal > 0 ? 0 : 1);
                        setValue = cVal > 0 ? 1 : 0UL;
                        break;
                    default:
                        Service.Chat.PrintError($"/setoption {optionKind} ({optionTypeValueHints[optionDefinition.OptionType]})");
                        break;
                }

                break;
            }
            case OptionType.NameDisplayModeBattle: {
                switch (optionValue.ToLowerInvariant()) {
                    case "a":
                    case "always":
                        configModule->SetOptionById(optionDefinition.ID, 0);
                        break;
                    case "b":
                    case "battle":
                        configModule->SetOptionById(optionDefinition.ID, 1);
                        break;
                    case "t":
                    case "target":
                    case "targeted":
                        configModule->SetOptionById(optionDefinition.ID, 2);
                        break;
                    case "n":
                    case "never": 
                        configModule->SetOptionById(optionDefinition.ID, 3);
                        break;
                    default:
                        Service.Chat.PrintError($"/setoption {optionKind} ({optionTypeValueHints[optionDefinition.OptionType]})");
                        break;
                }
                break;
            }
            default:
                Service.Chat.PrintError("Unsupported Option");
                return;
        }

        switch (optionDefinition.OptionType) {
            case OptionType.ToggleGamepadMode: {
                setGamepadMode(configModule, setValue);
                break;
            }
        }
            
    }

    public override void Disable() {
        Service.Commands.RemoveHandler("/setoption");
        Service.Commands.RemoveHandler("/setopt");
        Enabled = false;
    }

    public override void Dispose() {
        Enabled = false;
        Ready = false;
    }
}