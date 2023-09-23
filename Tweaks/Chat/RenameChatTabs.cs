using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.Chat;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using AlignmentType = FFXIVClientStructs.FFXIV.Component.GUI.AlignmentType;

namespace SimpleTweaksPlugin {
    public partial class  ChatTweaksConfig {
        public bool ShouldSerializeRenameChatTabs() => RenameChatTabs != null;
        public RenameChatTabs.Config RenameChatTabs = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Chat {
    public class RenameChatTabs : ChatTweaks.SubTweak {

        public class Config : TweakConfig {
            public bool DoRenameTab0;
            public bool DoRenameTab1;
            public string ChatTab0Name = string.Empty;
            public string ChatTab1Name = string.Empty;
        }

        public override string Name => "Rename Chat Tabs";
        public override string Description => "Allows renaming the General and Battle tabs in the chat window.";

        private Task renameTask;
        private CancellationTokenSource cancellationToken;

        public Config TweakConfig { get; private set; }

        protected override void Enable() {
            if (Enabled) return;
            TweakConfig = LoadConfig<Config>() ?? Plugin.PluginConfig.ChatTweaks.RenameChatTabs ?? new Config();

            Service.ClientState.Login += OnLogin;
            if (Service.ClientState.LocalPlayer != null) {
                OnLogin();
            }
            base.Enable();
        }

        protected override void Disable() {
            SaveConfig(TweakConfig);
            PluginConfig.ChatTweaks.RenameChatTabs = null;
            Service.ClientState.Login -= OnLogin;
            cancellationToken?.Cancel();
            if (renameTask != null) {
                var c = 0;
                while (!renameTask.IsCompleted) {
                    Thread.Sleep(1);
                    if (!renameTask.IsCompleted && c % 1000 == 0) SimpleLog.Verbose("Waiting for task to complete");
                    c++;
                }
            }

            ResetName();
            base.Disable();
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("###enabledRenameTab0", ref TweakConfig.DoRenameTab0);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputTextWithHint(LocString("TabLabel", "Tab {0}").Format(1) + "###nameTab0", DefaultName0, ref TweakConfig.ChatTab0Name, 16);

            hasChanged |= ImGui.Checkbox("###enabledRenameTab1", ref TweakConfig.DoRenameTab1);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputTextWithHint(LocString("TabLabel", "Tab {0}").Format(2) + "###nameTab1", DefaultName1, ref TweakConfig.ChatTab1Name, 16);
        };

        private void OnLogin() {
            DoRename();
        }

        public unsafe void DoRename() {

            if (renameTask != null && !renameTask.IsCompleted) { return; }

            cancellationToken = new CancellationTokenSource();

            renameTask = Task.Run(() => {
                while (true) {
                    try {
                        if (cancellationToken.IsCancellationRequested) break;
                        var chatLog = (AtkUnitBase*) Service.GameGui.GetAddonByName("ChatLog", 1);
                        if (chatLog != null) {
                            DoRename(chatLog);

                            var chatLogPanel2 = (AtkUnitBase*) Service.GameGui.GetAddonByName("ChatLogPanel_1", 1);
                            if (chatLogPanel2 != null) {
                                DoRenamePanel(chatLogPanel2);
                            }
                        }
                        cancellationToken.Token.WaitHandle.WaitOne(1000);
                    } catch (Exception ex) {
                        SimpleLog.Error(ex);
                        cancellationToken.Token.WaitHandle.WaitOne(10000);
                    }
                }
            }, cancellationToken.Token);
        }

        public unsafe void ResetName() {
            var chatLog = (AtkUnitBase*) Service.GameGui.GetAddonByName("ChatLog", 1);
            if (chatLog != null) DoRename(chatLog, true);
            var chatLogPanel = (AtkUnitBase*) Service.GameGui.GetAddonByName("ChatLogPanel_1", 1);
            if (chatLogPanel != null) DoRenamePanel(chatLogPanel, true);
        }

        public string DefaultName0 => Service.ClientState.ClientLanguage switch {
            ClientLanguage.French => "Général",
            ClientLanguage.German => "Allgemein",
            _ => "General"
        };
        public string DefaultName1 => Service.ClientState.ClientLanguage switch {
            ClientLanguage.French => "Combat",
            ClientLanguage.German => "Kampf",
            _ => "Battle"
        };
        
        public unsafe void DoRename(AtkUnitBase* unitBase, bool reset = false) {
            if (unitBase == null) return;
            if (unitBase->UldManager.NodeListCount < 14) return;
            SetTabName((AtkComponentNode*) unitBase->UldManager.NodeList[13], (reset || !TweakConfig.DoRenameTab0 || string.IsNullOrEmpty(TweakConfig.ChatTab0Name)) ? DefaultName0 : TweakConfig.ChatTab0Name);
            SetTabName((AtkComponentNode*) unitBase->UldManager.NodeList[12], (reset || !TweakConfig.DoRenameTab1 || string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) ? DefaultName1 : TweakConfig.ChatTab1Name);
            
            // Redo Positions
            ushort x = (ushort) (23 * unitBase->UldManager.NodeList[13]->ScaleX);
            for (var i = 13; i > 3; i--) {
                if (i == 5) continue;
                var t = unitBase->UldManager.NodeList[i];
                if (!t->NodeFlags.HasFlag(NodeFlags.Visible)) continue;
                t->X = x;
                t->Flags_2 |= 0x1;
                x += (ushort) (t->Width * t->ScaleX);
            }
        }

        public unsafe void DoRenamePanel(AtkUnitBase* panel, bool reset = false) {
            if (panel->UldManager.NodeListCount < 6) return;
            var baseComponent = (AtkComponentNode*) panel->UldManager.NodeList[5];
            if (baseComponent == null) return;
            if (baseComponent->Component->UldManager.NodeListCount < 2) return;
            var textNode = (AtkTextNode*) baseComponent->Component->UldManager.NodeList[1];
            if (textNode == null) return;

            var name = $"{(char) SeIconChar.BoxedNumber2} {((reset || !TweakConfig.DoRenameTab1 || string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) ? DefaultName1 : TweakConfig.ChatTab1Name)}";
            var str = Common.ReadSeString(textNode->NodeText.StringPtr);
            if (str.TextValue == name) return;
            SimpleLog.Log($"Rename Panel: '{str.TextValue}' -> '{name}'");
            
            textNode->AtkResNode.Width = 0; // Auto resizing only grows the box. Set to zero to guarantee it.
            textNode->AlignmentFontType = (byte) AlignmentType.Left; // Auto resizing doesn't work on Center aligned text.
            textNode->SetText(name);
            textNode->AtkResNode.Width += 5;
            textNode->AtkResNode.Flags_2 |= 0x1;

            baseComponent->Component->UldManager.NodeList[0]->Width = (ushort)(textNode->AtkResNode.Width + 6);
            baseComponent->Component->UldManager.NodeList[0]->Flags_2 |= 0x1;
            baseComponent->AtkResNode.Width = (ushort) (textNode->AtkResNode.Width + 6);
            baseComponent->AtkResNode.Flags_2 |= 0x1;

            panel->UldManager.NodeList[4]->X = 29 + textNode->AtkResNode.Width;
            panel->UldManager.NodeList[4]->Flags_2 |= 0x1;
            
            if (baseComponent->Component->UldManager.NodeListCount < 3) return;
            baseComponent->Component->UldManager.NodeList[0]->Width = baseComponent->AtkResNode.Width;
            baseComponent->Component->UldManager.NodeList[2]->Width = baseComponent->AtkResNode.Width;
        }


        public unsafe void SetTabName(AtkComponentNode* tab, string name) {
            if (tab == null) return;
            if (tab->Component->UldManager.NodeListCount < 4) return;
            var textNode = (AtkTextNode*)tab->Component->UldManager.NodeList[3];
            if (textNode == null) return;
            var str = Common.ReadSeString(textNode->NodeText.StringPtr);
            if (str.TextValue == name && textNode->AtkResNode.Width < 1000) return;
            SimpleLog.Log($"Rename Tab: '{str.TextValue}' -> '{name}' [{textNode->AtkResNode.Width}]");
            textNode->AtkResNode.Width = 0;
            textNode->SetText(name);
            textNode->AtkResNode.Width += 10;
            if (textNode->AtkResNode.Width > 1000) textNode->AtkResNode.Width = 180;
            textNode->AtkResNode.Flags_2 |= 0x1;

            var tabWidth = (ushort) (textNode->AtkResNode.Width + 16);


            tab->Component->UldManager.NodeList[0]->Width = tabWidth;
            tab->Component->UldManager.NodeList[0]->Flags_2 |= 0x1;

            tab->Component->UldManager.NodeList[1]->Width = tabWidth;
            tab->Component->UldManager.NodeList[1]->Flags_2 |= 0x1;

            tab->Component->UldManager.NodeList[2]->Width = textNode->AtkResNode.Width;
            tab->Component->UldManager.NodeList[2]->Flags_2 |= 0x1;

            tab->AtkResNode.Width = tabWidth;
            tab->AtkResNode.Flags_2 |= 0x1;
        }

    }
}
