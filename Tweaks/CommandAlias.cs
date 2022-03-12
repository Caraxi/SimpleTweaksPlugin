using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class CommandAlias : Tweak {
    #region Config
    public class Config : TweakConfig {
        public override int Version { get; set; } = 2;
        public List<AliasEntry> AliasList = new();
    }
        
    public Config TweakConfig { get; private set; }
        

    public class AliasEntry {
        public static readonly string[] NoOverwrite = { "xlplugins", "xlsettings", "xldclose", "xldev", "tweaks" };
        public bool Enabled = true;
        public string Input = string.Empty;
        public string Output = string.Empty;
        [NonSerialized] public bool Delete;
        [NonSerialized] public int UniqueId;
        public bool IsValid() {
            if (NoOverwrite.Contains(Input)) return false;
            if (Input.Contains(' ')) return false;
            return !(string.IsNullOrWhiteSpace(Input) || string.IsNullOrWhiteSpace(Output));
        }

    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool change) => {
        ImGui.Text(LocString("Instruction", "Add list of command alias. Do not start command with the '/'\nThese aliases, by design, do not work with macros."));
        if (ImGui.IsItemHovered()) {
            ImGui.SetNextWindowSize(new Vector2(280, -1));
            ImGui.BeginTooltip();
            ImGui.TextWrapped(LocString("MacroHelp", "Aliases are not supported in macros to prevent them from being sent to the server in the event you back them up on server.\nPlease use the original command in your macros.", "Macro Help Tooltip"));
            ImGui.EndTooltip();
        }
        ImGui.Separator();
        ImGui.Columns(4);
        var s = ImGui.GetIO().FontGlobalScale;
        ImGui.SetColumnWidth(0, 60 * s );
        ImGui.SetColumnWidth(1, 150 * s );
        ImGui.SetColumnWidth(2, 150 * s );
        ImGui.Text(LocString("Enabled"));
        ImGui.NextColumn();
        ImGui.Text(LocString("Input Command"));
        ImGui.NextColumn();
        ImGui.Text(LocString("Output Command"));
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
    };
    #endregion

    public override string Name => "Command Alias";
    public override string Description => "Allows replacing commands typed into chat box with other commands.";

    private IntPtr processChatInputAddress;
    private unsafe delegate byte ProcessChatInputDelegate(IntPtr uiModule, byte** a2, IntPtr a3);

    private Hook<ProcessChatInputDelegate> processChatInputHook;

    public override void Setup() {
        if (Ready) return;
        try {
            processChatInputAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? FE 86 ?? ?? ?? ?? C7 86 ?? ?? ?? ?? ?? ?? ?? ??");
            Ready = true;
        } catch {
            SimpleLog.Log("Failed to find address for ProcessChatInput");
        }
    }

    public override unsafe void Enable() {
        if (!Ready) return;
        TweakConfig = LoadConfig<Config>() ?? new Config();

        if (TweakConfig.Version == 1) {
            // To avoid breaking old aliases that relied on the space, automatically add it when we would have removed it.
            foreach(var alias in TweakConfig.AliasList) {
                if (alias.Output.Contains(' ')) alias.Output = $"{alias.Output} ";
            }
            TweakConfig.Version = 2;
            SaveConfig(TweakConfig);
        }

        processChatInputHook ??= new Hook<ProcessChatInputDelegate>(processChatInputAddress, ProcessChatInputDetour);
        processChatInputHook?.Enable();
        Enabled = true;
    }

    private unsafe byte ProcessChatInputDetour(IntPtr uiModule, byte** message, IntPtr a3) {
        try {
            var bc = 0;
            for (var i = 0; i <= 500; i++) {
                if (*(*message + i) != 0) continue;
                bc = i;
                break;
            }
            if (bc < 2 || bc > 500) {
                return processChatInputHook.Original(uiModule, message, a3);
            }
                
            var inputString = Encoding.UTF8.GetString(*message, bc);
            if (inputString.StartsWith("/")) {
                var splitString = inputString.Split(' ');

                if (splitString.Length > 0 && splitString[0].Length >= 2) {
                    var alias = TweakConfig.AliasList.FirstOrDefault(a => {
                        if (!a.Enabled) return false;
                        if (!a.IsValid()) return false;
                        return splitString[0] == $"/{a.Input}";
                    });
                    if (alias != null) {
                        // https://git.sr.ht/~jkcclemens/CCMM/tree/master/Custom%20Commands%20and%20Macro%20Macros/GameFunctions.cs#L44
                        var commandExtra = inputString[(alias.Input.Length + 1)..];
                        if (commandExtra.StartsWith(' ')) commandExtra = commandExtra[1..];
                        var newStr = alias.Output.Contains(' ') ? $"/{alias.Output}{commandExtra}" : $"/{alias.Output} {commandExtra}";
                        if (newStr.Length <= 500) {
                            SimpleLog.Verbose($"Aliasing Command: {inputString} -> {newStr}");
                            var bytes = Encoding.UTF8.GetBytes(newStr);
                            var mem1 = Marshal.AllocHGlobal(400);
                            var mem2 = Marshal.AllocHGlobal(bytes.Length + 30);
                            Marshal.Copy(bytes, 0, mem2, bytes.Length);
                            Marshal.WriteByte(mem2 + bytes.Length, 0);
                            Marshal.WriteInt64(mem1, mem2.ToInt64());
                            Marshal.WriteInt64(mem1 + 8, 64);
                            Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
                            Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);
                            var r = processChatInputHook.Original(uiModule, (byte**) mem1.ToPointer(), a3);
                            Marshal.FreeHGlobal(mem1);
                            Marshal.FreeHGlobal(mem2);
                            return r;
                        }

                        Service.Chat.PrintError("[Simple Tweaks] " +  LocString("CommandTooLongError", "Command alias result is longer than the maximum of 500 characters. The command could not be executed.", "Error: Command is too long"));
                        return 0;
                    }
                }
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
            
        return processChatInputHook.Original(uiModule, message, a3);
    }
        
    public override void Disable() {
        SaveConfig(TweakConfig);
        processChatInputHook?.Disable();
        Enabled = false;
    }

    public override void Dispose() {
        if (!Ready) return;
        processChatInputHook?.Disable();
        processChatInputHook?.Dispose();
        Ready = false;
        Enabled = false;
    }
}