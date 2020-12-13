using System;
using System.Diagnostics;
using System.IO;
using Dalamud.Game.Command;
using ImGuiNET;

namespace SimpleTweaksPlugin.Tweaks {
    public class ChrDirCommand : Tweak {
        public override string Name => "Character Directory Command";

        public override void Setup() {
            Ready = true;
        }

        public override void Enable() {
            if (Enabled) return;
            PluginInterface.CommandManager.AddHandler("/chrdir", new CommandInfo(CommandHandler) {ShowInHelp = true, HelpMessage = "Print your character save directory to chat. /savedir open to open the save directory."});
            Enabled = true;
        }

        private void CommandHandler(string command, string arguments) {
            var saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "FINAL FANTASY XIV - A Realm Reborn", $"FFXIV_CHR{PluginInterface.ClientState.LocalContentId:X16}");
            if (arguments == "open") {
                Process.Start("explorer.exe", saveDir);
                return;
            }
            PluginInterface.Framework.Gui.Chat.Print(saveDir);
        }

        public override void DrawConfig(ref bool change) {
            if (!Enabled) {
                base.DrawConfig(ref change);
                return;
            }

            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {
                ImGui.TextDisabled("/chrdir");
                ImGui.TextDisabled("/chrdir open");
                ImGui.TreePop();
            }

        }

        public override void Disable() {
            PluginInterface.CommandManager.RemoveHandler("/chrdir");
            Enabled = false;
        }

        public override void Dispose() {
            Enabled = false;
            Ready = false;
        }
    }
}
