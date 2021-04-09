using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.Chat;

namespace SimpleTweaksPlugin {
    public partial class  ChatTweaksConfig {
        public RenameChatTabs.Config RenameChatTabs = new();
    }
}

namespace SimpleTweaksPlugin.Tweaks.Chat {
    public class RenameChatTabs : ChatTweaks.SubTweak {

        public class Config {
            public bool DoRenameTab0;
            public bool DoRenameTab1;
            public string ChatTab0Name = string.Empty;
            public string ChatTab1Name = string.Empty;
        }

        public override string Name => "Rename Chat Tabs";
        public override string Description => "Allows renaming the General and Battle tabs in the chat window.";

        private Task renameTask;
        private CancellationTokenSource cancellationToken;

        private Config TweakConfig => PluginConfig.ChatTweaks.RenameChatTabs;

        public override void Enable() {
            if (Enabled) return;

            PluginInterface.ClientState.OnLogin += OnLogin;
            if (PluginInterface.ClientState.LocalPlayer != null) {
                OnLogin(null, null);
            }
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.ClientState.OnLogin -= OnLogin;
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
            hasChanged |= ImGui.InputTextWithHint("Tab 1###nameTab0", DefaultName0, ref TweakConfig.ChatTab0Name, 16);

            hasChanged |= ImGui.Checkbox("###enabledRenameTab1", ref TweakConfig.DoRenameTab1);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputTextWithHint("Tab 2###nameTab1", DefaultName1, ref TweakConfig.ChatTab1Name, 16);
        };

        private void OnLogin(object sender, EventArgs e) {
            DoRename();
        }

        public unsafe void DoRename() {

            if (renameTask != null && !renameTask.IsCompleted) { return; }

            cancellationToken = new CancellationTokenSource();

            renameTask = Task.Run(() => {
                while (true) {
                    try {
                        if (cancellationToken.IsCancellationRequested) break;
                        var chatLog = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("ChatLog", 1);
                        if (chatLog != null) {
                            DoRename(chatLog);

                            var chatLogPanel2 = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("ChatLogPanel_1", 1);
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
            var chatLog = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("ChatLog", 1);
            if (chatLog != null) DoRename(chatLog, true);
            var chatLogPanel = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("ChatLogPanel_1", 1);
            if (chatLogPanel != null) DoRenamePanel(chatLogPanel, true);
        }

        public string DefaultName0 => PluginInterface.ClientState.ClientLanguage switch {
            ClientLanguage.French => "Général",
            ClientLanguage.German => "Allgemein",
            _ => "General"
        };
        public string DefaultName1 => PluginInterface.ClientState.ClientLanguage switch {
            ClientLanguage.French => "Combat",
            ClientLanguage.German => "Kampf",
            _ => "Battle"
        };
        
        public unsafe void DoRename(AtkUnitBase* unitBase, bool reset = false) {
            if (unitBase == null) return;
            if (unitBase->ULDData.NodeListCount < 14) return;
            SetTabName((AtkComponentNode*) unitBase->ULDData.NodeList[13], (reset || !TweakConfig.DoRenameTab0 || string.IsNullOrEmpty(TweakConfig.ChatTab0Name)) ? DefaultName0 : TweakConfig.ChatTab0Name);
            SetTabName((AtkComponentNode*) unitBase->ULDData.NodeList[12], (reset || !TweakConfig.DoRenameTab1 || string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) ? DefaultName1 : TweakConfig.ChatTab1Name);
            
            // Redo Positions
            ushort x = 23;
            for (var i = 13; i > 3; i--) {
                if (i == 5) continue;
                var t = unitBase->ULDData.NodeList[i];
                if ((t->Flags & 0x10) != 0x10) continue;
                t->X = x;
                t->Flags_2 |= 0x1;
                x += t->Width;
            }
        }

        public unsafe void DoRenamePanel(AtkUnitBase* panel, bool reset = false) {
            if (panel->ULDData.NodeListCount < 6) return;
            var baseComponent = (AtkComponentNode*) panel->ULDData.NodeList[5];
            if (baseComponent == null) return;
            if (baseComponent->Component->ULDData.NodeListCount < 2) return;
            var textNode = (AtkTextNode*) baseComponent->Component->ULDData.NodeList[1];
            if (textNode == null) return;

            var name = $"{(char) SeIconChar.BoxedNumber2} {((reset || !TweakConfig.DoRenameTab1 || string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) ? DefaultName1 : TweakConfig.ChatTab1Name)}";
            var str = Plugin.Common.ReadSeString(textNode->NodeText.StringPtr);
            if (str.TextValue == name) return;
            SimpleLog.Log($"Rename Panel: '{str.TextValue}' -> '{name}'");
            
            textNode->AtkResNode.Width = 0; // Auto resizing only grows the box. Set to zero to guarantee it.
            textNode->AlignmentFontType = (byte) AlignmentType.Left; // Auto resizing doesn't work on Center aligned text.
            UiHelper.SetText(textNode, $"{name}");
            textNode->AtkResNode.Width += 5;
            textNode->AtkResNode.Flags_2 |= 0x1;

            baseComponent->Component->ULDData.NodeList[0]->Width = (ushort)(textNode->AtkResNode.Width + 6);
            baseComponent->Component->ULDData.NodeList[0]->Flags_2 |= 0x1;
            baseComponent->AtkResNode.Width = (ushort) (textNode->AtkResNode.Width + 6);
            baseComponent->AtkResNode.Flags_2 |= 0x1;

            panel->ULDData.NodeList[4]->X = 29 + textNode->AtkResNode.Width;
            panel->ULDData.NodeList[4]->Flags_2 |= 0x1;
            
            if (baseComponent->Component->ULDData.NodeListCount < 3) return;
            baseComponent->Component->ULDData.NodeList[0]->Width = baseComponent->AtkResNode.Width;
            baseComponent->Component->ULDData.NodeList[2]->Width = baseComponent->AtkResNode.Width;
        }


        public unsafe void SetTabName(AtkComponentNode* tab, string name) {
            if (tab == null) return;
            if (tab->Component->ULDData.NodeListCount < 4) return;
            var textNode = (AtkTextNode*)tab->Component->ULDData.NodeList[3];
            if (textNode == null) return;
            var str = Plugin.Common.ReadSeString(textNode->NodeText.StringPtr);
            if (str.TextValue == name && textNode->AtkResNode.Width < 1000) return;
            SimpleLog.Log($"Rename Tab: '{str.TextValue}' -> '{name}' [{textNode->AtkResNode.Width}]");
            textNode->AtkResNode.Width = 0;
            UiHelper.SetText(textNode, name);
            textNode->AtkResNode.Width += 10;
            if (textNode->AtkResNode.Width > 1000) textNode->AtkResNode.Width = 180;
            textNode->AtkResNode.Flags_2 |= 0x1;

            var tabWidth = (ushort) (textNode->AtkResNode.Width + 16);


            tab->Component->ULDData.NodeList[0]->Width = tabWidth;
            tab->Component->ULDData.NodeList[0]->Flags_2 |= 0x1;

            tab->Component->ULDData.NodeList[1]->Width = tabWidth;
            tab->Component->ULDData.NodeList[1]->Flags_2 |= 0x1;

            tab->Component->ULDData.NodeList[2]->Width = textNode->AtkResNode.Width;
            tab->Component->ULDData.NodeList[2]->Flags_2 |= 0x1;

            tab->AtkResNode.Width = tabWidth;
            tab->AtkResNode.Flags_2 |= 0x1;
        }

    }
}
