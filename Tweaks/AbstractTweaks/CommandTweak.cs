using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.AbstractTweaks;

[TweakCategory(TweakCategory.Command)]
public abstract class CommandTweak : Tweak {
    protected abstract string Command { get; }
    protected string CustomOrDefaultCommand => customMainCommand.IsNullOrEmpty() ? Command.TrimStart('/') : customMainCommand.TrimStart('/');
    protected virtual string[] Alias => [];
    protected virtual string HelpMessage => $"[{Plugin.Name} {Name}";
    protected virtual bool ShowInHelp => true;

    protected abstract void OnCommand(string args);

    private void OnCommandInternal(string _, string args) => OnCommand(args);

    private readonly List<string> registeredCommands = [];
    private string customMainCommand = string.Empty;
    private string customMainCommandInput = string.Empty;


    [StructLayout(LayoutKind.Explicit, Size = 0x1250)]
    private struct RaptureShellModuleExt {
        [FieldOffset(0x00)] public RaptureShellModule RaptureShellModule;
        [FieldOffset(0x2B2)] private byte suppressMacroErrors;

        public int MacroCurrentLine => RaptureShellModule.MacroCurrentLine;
        public bool SuppressMacroErrors => suppressMacroErrors != 0;
    }
    protected unsafe bool ShowCommandErrors {
        get {
            try {
                var shellModule = (RaptureShellModuleExt*) RaptureShellModule.Instance();
                return shellModule->MacroCurrentLine < 0 || !shellModule->SuppressMacroErrors;
            } catch {
                return true;
            }
        }
    }

    protected sealed override void Enable() {
        EnableCommand();
        customMainCommand = string.Empty;
        customMainCommandInput = string.Empty;
        if (PluginConfig.CustomizedCommands.TryGetValue(Key, out var customCommand)) {
            customCommand = customCommand.Split(' ')[0];
            if (!customCommand.StartsWith('/')) customCommand = $"/{customCommand}";
            if (customCommand.Length >= 2) {
                customMainCommand = customCommand;
                customMainCommandInput = customCommand;
            }
        }

        var command = string.IsNullOrWhiteSpace(customMainCommand) ? Command : customMainCommand;

        var c = command.StartsWith("/") ? command : $"/{command}";
        if (Service.Commands.Commands.ContainsKey(c)) {
            Plugin.Error(this, new Exception($"Command '{c}' is already registered."));
        } else {
            Service.Commands.AddHandler(c, new CommandInfo(OnCommandInternal) { HelpMessage = HelpMessage, ShowInHelp = ShowInHelp });

            registeredCommands.Add(c);
        }

        if (!PluginConfig.DisabledCommandAlias.TryGetValue(Key, out var disabledAlias)) {
            disabledAlias = new List<string>();
        }

        foreach (var a in Alias) {
            if (disabledAlias.Contains(a)) continue;
            var alias = a.StartsWith("/") ? a : $"/{a}";
            if (!Service.Commands.Commands.ContainsKey(alias)) {
                Service.Commands.AddHandler(alias, new CommandInfo(OnCommandInternal) { HelpMessage = HelpMessage, ShowInHelp = false });
                registeredCommands.Add(alias);
            }
        }
    }

    protected sealed override void Disable() {
        DisableCommand();
        foreach (var c in registeredCommands) {
            Service.Commands.RemoveHandler(c);
        }

        registeredCommands.Clear();
    }

    protected List<string> GetArgumentList(string args) =>
        Regex.Matches(args, @"[\""].+?[\""]|[^ ]+").Select(m => {
            if (m.Value.StartsWith('"') && m.Value.EndsWith('"')) {
                return m.Value.Substring(1, m.Value.Length - 2);
            }

            return m.Value;
        }).ToList();

    public void DrawCommandEditor(bool withTree = true) {
        var show = !withTree || ImGui.TreeNode($"Command Customization##{Key}");
        if (!show) return;

        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint($"##mainCommand{Key}", Command.StartsWith('/') ? Command : $"/{Command}", ref customMainCommandInput, 32);

        ImGui.SameLine();
        ImGui.BeginDisabled(customMainCommand == customMainCommandInput || customMainCommandInput.Contains(' '));
        if (ImGui.Button($"Apply##commandEdit_{Key}")) {
            if (string.IsNullOrWhiteSpace(customMainCommandInput)) {
                customMainCommand = string.Empty;
                customMainCommandInput = string.Empty;
                PluginConfig.CustomizedCommands.Remove(Key);
                PluginConfig.Save();
            } else {
                if (!customMainCommandInput.StartsWith('/')) customMainCommandInput = $"/{customMainCommandInput}";

                customMainCommand = customMainCommandInput;
                PluginConfig.CustomizedCommands.Remove(Key);
                PluginConfig.CustomizedCommands.Add(Key, customMainCommandInput);
                PluginConfig.Save();

                if (Enabled) {
                    Disable();
                    Enable();
                }
            }
        }

        ImGui.EndDisabled();
        ImGui.SameLine();
        if (customMainCommandInput.Contains(' ')) {
            ImGui.TextDisabled("Command must not contain any spaces.");
        } else {
            ImGui.Text("Main Command");
        }

        if (Alias.Length > 0) {
            ImGui.Text("Aliases:");
            ImGui.Indent();
            if (!PluginConfig.DisabledCommandAlias.TryGetValue(Key, out var disabledCommandAlias)) {
                disabledCommandAlias = new List<string>();
            }

            foreach (var a in Alias) {
                var isEnabled = !disabledCommandAlias.Contains(a);
                if (ImGui.Checkbox(a.StartsWith('/') ? a : $"/{a}##commandAliasToggle_{a}_{Key}", ref isEnabled)) {
                    if (isEnabled) {
                        disabledCommandAlias.Remove(a);
                    } else {
                        disabledCommandAlias.Add(a);
                    }

                    PluginConfig.DisabledCommandAlias.Remove(Key);
                    PluginConfig.DisabledCommandAlias.Add(Key, disabledCommandAlias);
                    if (Enabled) {
                        Disable();
                        Enable();
                    }
                }
            }

            ImGui.Unindent();
        }

        if (withTree) ImGui.TreePop();
    }

    protected virtual void EnableCommand() { }
    protected virtual void DisableCommand() { }
}
