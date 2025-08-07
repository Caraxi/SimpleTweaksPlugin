using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakCategory(TweakCategory.Command)]
[TweakName("Command Alias")]
[TweakDescription("Allows replacing commands typed into chat box with other commands.")]
[TweakAutoConfig]
public class CommandAlias : Tweak {
    #region Config

    public class Config : TweakConfig {
        public override int Version { get; set; } = 2;
        public List<AliasEntry> AliasList = new();
    }

    public Config TweakConfig { get; private set; }

    public class AliasEntry {
        public static readonly string[] NoOverwrite = ["xlplugins", "xlsettings", "xldclose", "xldev", "tweaks"];
        public bool Enabled = true;
        public string Input = string.Empty;
        public string Output = string.Empty;
        public bool Resend;
        [NonSerialized] public bool Delete;
        [NonSerialized] public int UniqueId;

        public bool IsValid() {
            if (NoOverwrite.Contains(Input)) return false;
            if (Input.Contains(' ')) return false;
            return !(string.IsNullOrWhiteSpace(Input) || string.IsNullOrWhiteSpace(Output));
        }
    }
    
    private Stopwatch resendSafety = Stopwatch.StartNew();
    
    protected void DrawConfig(ref bool change) {
        ImGui.Text(LocString("Instruction", "Add list of command alias. Do not start command with the '/'\nThese aliases, by design, do not work with macros."));
        if (ImGui.IsItemHovered()) {
            ImGui.SetNextWindowSize(new Vector2(280, -1));
            ImGui.BeginTooltip();
            ImGui.TextWrapped(LocString("MacroHelp", "Aliases are not supported in macros to prevent them from being sent to the server in the event you back them up on server.\nPlease use the original command in your macros.", "Macro Help Tooltip"));
            ImGui.EndTooltip();
        }

        ImGui.Separator();
        ImGui.Columns(5);
        var s = ImGui.GetIO().FontGlobalScale;
        ImGui.SetColumnWidth(0, 60 * s);
        ImGui.SetColumnWidth(1, 150 * s);
        ImGui.SetColumnWidth(2, 150 * s);
        ImGui.SetColumnWidth(3, 50 * s);
        ImGui.Text(LocString("\nEnabled"));
        ImGui.NextColumn();
        ImGui.Text(LocString("\nInput Command"));
        ImGui.NextColumn();
        ImGui.Text(LocString("\nOutput Command"));
        ImGui.NextColumn();
        ImGui.Text(LocString("Alt\nMode"));
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Use an alternative method to send the alias.");
        }
        ImGui.NextColumn();
        ImGui.NextColumn();
        ImGui.Separator();

        foreach (var aliasEntry in TweakConfig.AliasList) {
            if (aliasEntry.UniqueId == 0) {
                aliasEntry.UniqueId = TweakConfig.AliasList.Max(a => a.UniqueId) + 1;
            }

            ImGui.Separator();
            if (aliasEntry.IsValid()) {
                change = ImGui.Checkbox($"###aliasToggle{aliasEntry.UniqueId}", ref aliasEntry.Enabled) || change;
            } else {
                ImGui.Text("Invalid");
            }

            ImGui.NextColumn();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.Text("/");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-5);
            change |= ImGui.InputText($"###aliasInput{aliasEntry.UniqueId}", ref aliasEntry.Input, 500) || change;
            ImGui.PopStyleVar();
            ImGui.NextColumn();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.Text("/");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-5);
            change |= ImGui.InputText($"###aliasOutput{aliasEntry.UniqueId}", ref aliasEntry.Output, 500) || change;
            ImGui.PopStyleVar();
            ImGui.NextColumn();

            ImGui.Checkbox($"###altMode{aliasEntry.UniqueId}", ref aliasEntry.Resend);
            
            ImGui.NextColumn();
            if (AliasEntry.NoOverwrite.Contains(aliasEntry.Input)) {
                var f = LocString("ProtectedCommandError", "'/{0}' is a protected command.");
                ImGui.TextColored(new Vector4(1, 0, 0, 1), string.Format(f, aliasEntry.Input));
            } else if (string.IsNullOrEmpty(aliasEntry.Input)) {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), LocString("EmptyInputError", "Input must not be empty."));
            } else if (string.IsNullOrEmpty(aliasEntry.Output)) {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), LocString("EmptyOutputError", "Output must not be empty."));
            } else if (aliasEntry.Input.StartsWith("/")) {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), LocString("SlashIncludedError", "Don't include the '/'"));
            } else if (aliasEntry.Input.Contains(' ')) {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), LocString("SpaceInInput", "Input Command cannot contain a space."));
            }

            ImGui.NextColumn();

            if (string.IsNullOrWhiteSpace(aliasEntry.Input) && string.IsNullOrWhiteSpace(aliasEntry.Output)) {
                aliasEntry.Delete = true;
            }
        }

        if (TweakConfig.AliasList.Count > 0 && TweakConfig.AliasList.RemoveAll(a => a.Delete) > 0) {
            change = true;
        }

        ImGui.Separator();
        var addNew = false;
        var newEntry = new AliasEntry() { UniqueId = TweakConfig.AliasList.Count == 0 ? 1 : TweakConfig.AliasList.Max(a => a.UniqueId) + 1 };
        ImGui.Text(LocString("New Label", "New:"));
        ImGui.NextColumn();
        ImGui.SetNextItemWidth(-1);
        addNew = ImGui.InputText($"###aliasInput{newEntry.UniqueId}", ref newEntry.Input, 500) || addNew;
        ImGui.NextColumn();
        ImGui.SetNextItemWidth(-1);
        addNew = ImGui.InputText($"###aliasOutput{newEntry.UniqueId}", ref newEntry.Output, 500) || addNew;
        ImGui.NextColumn();

        if (addNew) {
            TweakConfig.AliasList.Add(newEntry);
            change = true;
        }

        ImGui.Columns(1);
    }

    #endregion

    [TweakHook(typeof(ShellCommandModule), nameof(ShellCommandModule.ExecuteCommandInner), nameof(ProcessChatInputDetour))]
    private HookWrapper<ShellCommandModule.Delegates.ExecuteCommandInner> processChatInputHook;

    private unsafe void ProcessChatInputDetour(ShellCommandModule* shellCommandModule, Utf8String* message, UIModule* uiModule) {
        try {
            if (message->GetCharAt(0) == '/') {
                var inputString = message->ToString();
                var splitString = inputString.Split(' ');
                if (splitString.Length > 0 && splitString[0].Length >= 2) {
                    var alias = TweakConfig.AliasList.FirstOrDefault(a => {
                        if (!a.Enabled) return false;
                        if (!a.IsValid()) return false;
                        return splitString[0] == $"/{a.Input}";
                    });

                    if (alias == null && Plugin.GetTweak<Chat.CaseInsensitiveCommands>().Enabled) {
                        alias = TweakConfig.AliasList.FirstOrDefault(a => a.Enabled && a.IsValid() && splitString[0].Equals($"/{a.Input}", StringComparison.InvariantCultureIgnoreCase));
                    }

                    if (alias != null) {
                        var commandExtra = inputString[(alias.Input.Length + 1)..];
                        if (commandExtra.StartsWith(' ')) commandExtra = commandExtra[1..];
                        var newStr = alias.Output.Contains(' ') ? $"/{alias.Output}{commandExtra}" : $"/{alias.Output} {commandExtra}";
                        if (newStr.Length <= 500) {
                            if (alias.Resend) {
                                if (resendSafety.ElapsedMilliseconds >= 1000) {
                                    resendSafety.Restart();
                                    ChatHelper.SendMessage(newStr);
                                } else {
                                    Service.Chat.PrintError("[Simple Tweaks] Something went wrong... You seem to have a command loop");
                                }
                                return;
                            }
                            
                            var str = Utf8String.FromString(newStr);
                            processChatInputHook.Original(shellCommandModule, str, uiModule);
                            str->Dtor(true);
                            return;
                        }

                        Service.Chat.PrintError("[Simple Tweaks] " + LocString("CommandTooLongError", "Command alias result is longer than the maximum of 500 characters. The command could not be executed.", "Error: Command is too long"));
                    }
                }
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }

        processChatInputHook.Original(shellCommandModule, message, uiModule);
    }
}
