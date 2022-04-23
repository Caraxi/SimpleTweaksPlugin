using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class SetOptionCommand : Tweak {

    public override string Name => "Set Option Command";
    public override string Description => "Adds commands to change various settings.";

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)] 
    private delegate void DispatchEvent(AgentInterface* agentConfigCharacter, void* outVal, AtkValue* atkValue, uint atkValueCount);

    private DispatchEvent dispatchEvent;

    public enum OptionType {
        Bool,
        ToggleGamepadMode, // bool with extra shit
        NameDisplayModeBattle,
        NameDisplayMode,
    }

    public class OptionDefinition {
        public string Name { get; }
        public ConfigOption ID { get; }
        public OptionType OptionType { get; }
        public string[] Alias { get; }

        public OptionDefinition(string name, ConfigOption id, OptionType type, params string[] alias) {
            this.Name = name;
            this.ID = id;
            this.OptionType = type;
            this.Alias = alias;
        }

    }

    private readonly List<OptionDefinition> optionDefinitions = new() {
        new OptionDefinition("GamepadMode", ConfigOption.GamepadMode, OptionType.ToggleGamepadMode, "gp"),

        new OptionDefinition("ItemTooltips", ConfigOption.DisplayItemHelp, OptionType.Bool, "itt"),
        new OptionDefinition("ActionTooltips", ConfigOption.DisplayActionHelp, OptionType.Bool, "att"),
        new OptionDefinition("LegacyMovement", ConfigOption.LegacyMovement, OptionType.Bool, "lm"),

        new OptionDefinition("OwnDisplayName", ConfigOption.OwnDisplayNameSettings, OptionType.NameDisplayModeBattle, "odn"),
        new OptionDefinition("PartyDisplayName", ConfigOption.PartyDisplayNameSettings, OptionType.NameDisplayModeBattle, "pdn"),
        new OptionDefinition("AllianceDisplayName", ConfigOption.AllianceDisplayNameSettings, OptionType.NameDisplayModeBattle, "adn"),
        new OptionDefinition("OtherPlayerDisplayName", ConfigOption.OtherPCsDisplayNameSettings, OptionType.NameDisplayModeBattle, "opcdn"),
        new OptionDefinition("FriendDisplayName", ConfigOption.FriendsDisplayNameSettings, OptionType.NameDisplayModeBattle, "fdn"),
    };


    private readonly Dictionary<OptionType, string> optionTypeValueHints = new Dictionary<OptionType, string> {
        {OptionType.Bool, "on | off | toggle"},
        {OptionType.ToggleGamepadMode, "on | off | toggle"},
        {OptionType.NameDisplayModeBattle, "always | battle | targeted | never"},
    };
        
    public override void Setup() {
        if (Ready) return;

        try {
            var dispatchEventPtr = Service.SigScanner.ScanText("48 89 5C 24 ?? 55 56 57 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4C 8B BC 24");
            SimpleLog.Verbose($"DispatchEventPtr: {dispatchEventPtr.ToInt64():X}");
            dispatchEvent = Marshal.GetDelegateForFunctionPointer<DispatchEvent>(dispatchEventPtr);

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
            case OptionType.Bool: {
                switch (optionValue) {
                    case "1":
                    case "true":
                    case "on":
                        configModule->SetOption(optionDefinition.ID, 1);
                        break;
                    case "0":
                    case "false":
                    case "off":
                        configModule->SetOption(optionDefinition.ID, 0);
                        break;
                    case "":
                    case "t":
                    case "toggle":
                        var cVal = configModule->GetIntValue(optionDefinition.ID);
                        configModule->SetOption(optionDefinition.ID, cVal > 0 ? 0 : 1);
                        break;
                    default:
                        Service.Chat.PrintError($"/setoption {optionKind} ({optionTypeValueHints[optionDefinition.OptionType]})");
                        break;
                }

                break;
            }
            case OptionType.ToggleGamepadMode: {

                void SetGamepadMode(bool enabled) {
                    var values = Common.CreateAtkValueArray(19, 0, enabled ? 1 : 0, 0);
                    var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ConfigCharacter);
                    if (values != null && agent != null) {
                        dispatchEvent(agent, Common.ThrowawayOut, values, 4);
                    }
                    
                    if (values != null) Marshal.FreeHGlobal(new IntPtr(values));
                }
                
                switch (optionValue) {
                    case "1":
                    case "true":
                    case "on":
                        SetGamepadMode(true);
                        break;
                    case "0":
                    case "false":
                    case "off":
                        SetGamepadMode(false);
                        break;
                    case "":
                    case "t":
                    case "toggle":
                        SetGamepadMode(configModule->GetIntValue(optionDefinition.ID) == 0);
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
                        configModule->SetOption(optionDefinition.ID, 0);
                        break;
                    case "b":
                    case "battle":
                        configModule->SetOption(optionDefinition.ID, 1);
                        break;
                    case "t":
                    case "target":
                    case "targeted":
                        configModule->SetOption(optionDefinition.ID, 2);
                        break;
                    case "n":
                    case "never": 
                        configModule->SetOption(optionDefinition.ID, 3);
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