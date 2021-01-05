using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public CommandAlias.Config CommandAlias = new CommandAlias.Config();
    }
}

namespace SimpleTweaksPlugin.Tweaks {
    public class CommandAlias : Tweak {
        #region Config
        public class Config {
            public List<AliasEntry> AliasList = new List<AliasEntry>();
        }

        public class AliasEntry {
            public static readonly string[] NoOverwrite = { "xlplugins", "xlsettings", "xldclose", "xldev", "tweaks" };
            public bool Enabled = true;
            public string Input = string.Empty;
            public string Output = string.Empty;
            [NonSerialized] public bool Delete = false;
            [NonSerialized] public int UniqueId = 0;
            public bool IsValid() {
                if (NoOverwrite.Contains(Input)) return false;
                return !(string.IsNullOrWhiteSpace(Input) || string.IsNullOrWhiteSpace(Output));
            }

        }

        public override void DrawConfig(ref bool change) {
            if (!Enabled) {
                base.DrawConfig(ref change);
                return;
            }

            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {

                ImGui.Text("Add list of command alias. Do not start command with the '/'");
                ImGui.Text("These aliases, by design, do not work with macros.");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetNextWindowSize(new Vector2(280, -1));
                    ImGui.BeginTooltip();
                    ImGui.TextWrapped("Aliases are not supported in macros to prevent them from being sent to the server in the event you back them up on server.\nPlease use the original command in your macros.");
                    ImGui.EndTooltip();
                }
                ImGui.Separator();
                ImGui.Columns(4);
                var s = ImGui.GetIO().FontGlobalScale;
                ImGui.SetColumnWidth(0, 60 * s );
                ImGui.SetColumnWidth(1, 150 * s );
                ImGui.SetColumnWidth(2, 150 * s );
                ImGui.Text("Enabled");
                ImGui.NextColumn();
                ImGui.Text("Input Command");
                ImGui.NextColumn();
                ImGui.Text("Output Command");
                ImGui.NextColumn();
                ImGui.NextColumn();
                ImGui.Separator();
                
                foreach (var aliasEntry in PluginConfig.CommandAlias.AliasList) {

                    if (aliasEntry.UniqueId == 0) {
                        aliasEntry.UniqueId = PluginConfig.CommandAlias.AliasList.Max(a => a.UniqueId) + 1;
                    }

                    var focused = false;
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
                    change = ImGui.InputText($"###aliasInput{aliasEntry.UniqueId}", ref aliasEntry.Input, 30) || change;
                    focused = ImGui.IsItemFocused();
                    ImGui.PopStyleVar();
                    ImGui.NextColumn();
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                    ImGui.Text("/");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(-5);
                    change = ImGui.InputText($"###aliasOutput{aliasEntry.UniqueId}", ref aliasEntry.Output, 30) || change;
                    focused = focused || ImGui.IsItemFocused();
                    ImGui.PopStyleVar();
                    ImGui.NextColumn();
                    
                    if (AliasEntry.NoOverwrite.Contains(aliasEntry.Input)) {

                        ImGui.TextColored(new Vector4(1, 0, 0, 1), $"'/{aliasEntry.Input}' is a protected command.");
                    } else if (string.IsNullOrEmpty(aliasEntry.Input)) {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "Input must not be empty.");
                    } else if (string.IsNullOrEmpty(aliasEntry.Output)) {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "Output must not be empty.");
                    } else if (aliasEntry.Input.StartsWith("/")) {
                        ImGui.TextColored(new Vector4(1, 1, 0, 1), "Don't include the '/'");
                    }

                    ImGui.NextColumn();

                    if (string.IsNullOrWhiteSpace(aliasEntry.Input) && string.IsNullOrWhiteSpace(aliasEntry.Output)) {
                        aliasEntry.Delete = true;
                    }
                }

                if (PluginConfig.CommandAlias.AliasList.Count > 0 && PluginConfig.CommandAlias.AliasList.RemoveAll(a => a.Delete) > 0) {
                    change = true;
                }

                ImGui.Separator();
                var addNew = false;
                var newEntry = new AliasEntry() { UniqueId = PluginConfig.CommandAlias.AliasList.Count == 0 ? 1 : PluginConfig.CommandAlias.AliasList.Max(a => a.UniqueId) + 1 };
                ImGui.Text("New:");
                ImGui.NextColumn();
                ImGui.SetNextItemWidth(-1);
                addNew = ImGui.InputText($"###aliasInput{newEntry.UniqueId}", ref newEntry.Input, 30) || addNew;
                ImGui.NextColumn();
                ImGui.SetNextItemWidth(-1);
                addNew = ImGui.InputText($"###aliasOutput{newEntry.UniqueId}", ref newEntry.Output, 30) || addNew;
                ImGui.NextColumn();

                if (addNew) {
                    PluginConfig.CommandAlias.AliasList.Add(newEntry);
                    change = true;
                }
                
                ImGui.Columns(1);
                ImGui.TreePop();
            }
        }
        #endregion

        public override string Name => "Command Alias";

        private IntPtr processChatInputAddress;
        private unsafe delegate byte ProcessChatInputDelegate(IntPtr uiModule, byte** a2, IntPtr a3);

        private Hook<ProcessChatInputDelegate> processChatInputHook;

        public override void Setup() {
            if (Ready) return;
            try {
                processChatInputAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? FE 86 ?? ?? ?? ?? C7 86 ?? ?? ?? ?? ?? ?? ?? ??");
                Ready = true;
            } catch {
                SimpleLog.Log("Failed to find address for ProcessChatInput");
            }
        }

        public override unsafe void Enable() {
            if (!Ready) return;
            processChatInputHook ??= new Hook<ProcessChatInputDelegate>(processChatInputAddress, new ProcessChatInputDelegate(ProcessChatInputDetour));
            processChatInputHook?.Enable();
            Enabled = true;
        }

        private unsafe byte ProcessChatInputDetour(IntPtr uiModule, byte** message, IntPtr a3) {
            try {
                var bc = *(short*) (message + 16) - 1;
                if (bc < 2 || bc > 255) {
                    return processChatInputHook.Original(uiModule, message, a3);
                }
                
                var inputString = Encoding.UTF8.GetString(*message, bc);
                if (inputString.StartsWith("/")) {
                    var splitString = inputString.Split(' ');

                    if (splitString.Length > 0 && splitString[0].Length >= 2) {
                        var alias = PluginConfig.CommandAlias.AliasList.FirstOrDefault(a => {
                            if (!a.Enabled) return false;
                            if (!a.IsValid()) return false;
                            return splitString[0] == $"/{a.Input}";
                        });
                        if (alias != null) {
                            // https://git.sr.ht/~jkcclemens/CCMM/tree/master/Custom%20Commands%20and%20Macro%20Macros/GameFunctions.cs#L44
                            var newStr = $"/{alias.Output}{inputString.Substring(alias.Input.Length + 1)}";
                            SimpleLog.Log($"Aliasing Command: {inputString} -> {newStr}");
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
                    }
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
            
            return processChatInputHook.Original(uiModule, message, a3);
        }
        
        public override void Disable() {
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
}
