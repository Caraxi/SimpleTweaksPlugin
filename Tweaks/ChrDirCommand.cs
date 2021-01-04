using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Command;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public class ChrDirCommand : Tweak {
        public override string Name => "Character Directory Command";

        public override void Setup() {
            Ready = true;
        }

        private DalamudLinkPayload linkPayload;

        public override void Enable() {
            if (Enabled) return;
            PluginInterface.CommandManager.AddHandler("/chrdir", new CommandInfo(CommandHandler) {ShowInHelp = true, HelpMessage = "Print your character save directory to chat. '/chrdir open' to open the directory in explorer."});

            linkPayload = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.OpenFolderLink, OpenFolder);
            
            Enabled = true;
        }

        private void OpenFolder(uint arg1, SeString arg2) {
            Process.Start("explorer.exe", arg2.TextValue);
        }

        private void CommandHandler(string command, string arguments) {
            var saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "FINAL FANTASY XIV - A Realm Reborn", $"FFXIV_CHR{PluginInterface.ClientState.LocalContentId:X16}");
            if (arguments == "open") {
                Process.Start("explorer.exe", saveDir);
                return;
            }
            PluginInterface.Framework.Gui.Chat.PrintChat(new XivChatEntry() {
                MessageBytes = new SeString(new List<Payload>() {
                    new TextPayload("Character Directory:\n"),
                    new UIForegroundPayload(PluginInterface.Data, 22),
                    linkPayload,
                    new TextPayload(saveDir),
                    RawPayload.LinkTerminator,
                    new UIForegroundPayload(PluginInterface.Data, 0)
                }).Encode()
            });
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
            PluginInterface.RemoveChatLinkHandler((uint) LinkHandlerId.OpenFolderLink);
            PluginInterface.CommandManager.RemoveHandler("/chrdir");
            Enabled = false;
        }

        public override void Dispose() {
            Enabled = false;
            Ready = false;
        }
    }
}
