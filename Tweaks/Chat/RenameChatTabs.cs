using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using AlignmentType = FFXIVClientStructs.FFXIV.Component.GUI.AlignmentType;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Rename Chat Tabs")]
[TweakDescription("Allows renaming the General and Battle tabs in the chat window.")]
[TweakAutoConfig]
public class RenameChatTabs : ChatTweaks.SubTweak {
    public class Config : TweakConfig {
        public bool DoRenameTab0;
        public bool DoRenameTab1;
        public string ChatTab0Name = string.Empty;
        public string ChatTab1Name = string.Empty;
    }

    private Task renameTask;
    private CancellationTokenSource cancellationToken;

    public Config TweakConfig { get; set; }

    protected override void Enable() {
        Service.ClientState.Login += OnLogin;
        if (Service.ClientState.LocalPlayer != null) OnLogin();
    }

    protected override void Disable() {
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
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("###enabledRenameTab0", ref TweakConfig.DoRenameTab0);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.InputTextWithHint(LocString("TabLabel", "Tab {0}").Format(1) + "###nameTab0", DefaultName0, ref TweakConfig.ChatTab0Name, 16);

        hasChanged |= ImGui.Checkbox("###enabledRenameTab1", ref TweakConfig.DoRenameTab1);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.InputTextWithHint(LocString("TabLabel", "Tab {0}").Format(2) + "###nameTab1", DefaultName1, ref TweakConfig.ChatTab1Name, 16);
    }

    private void OnLogin() => DoRename();

    public unsafe void DoRename() {
        if (renameTask is { IsCompleted: false }) {
            return;
        }

        cancellationToken = new CancellationTokenSource();

        renameTask = Task.Run(() => {
            while (true) {
                try {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (Common.GetUnitBase("ChatLog", out var chatLog)) {
                        DoRename(chatLog);
                        if (Common.GetUnitBase("ChatLogPanel_1", out var chatLogPanel)) {
                            DoRenamePanel(chatLogPanel);
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
        if (Common.GetUnitBase("ChatLog", out var chatLog)) DoRename(chatLog, true);
        if (Common.GetUnitBase("ChatLogPanel_1", out var chatLogPanel)) DoRename(chatLogPanel, true);
    }

    public string DefaultName0 =>
        Service.ClientState.ClientLanguage switch {
            ClientLanguage.French => "Général",
            ClientLanguage.German => "Allgemein",
            _ => "General"
        };

    public string DefaultName1 =>
        Service.ClientState.ClientLanguage switch {
            ClientLanguage.French => "Combat",
            ClientLanguage.German => "Kampf",
            _ => "Battle"
        };

    public unsafe void DoRename(AtkUnitBase* unitBase, bool reset = false) {
        if (unitBase == null) return;
        SetTabName((AtkComponentNode*)Common.GetNodeByID(unitBase, 7), (reset || !TweakConfig.DoRenameTab0 || string.IsNullOrEmpty(TweakConfig.ChatTab0Name)) ? DefaultName0 : TweakConfig.ChatTab0Name);
        SetTabName((AtkComponentNode*)Common.GetNodeByID(unitBase, 70001), (reset || !TweakConfig.DoRenameTab1 || string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) ? DefaultName1 : TweakConfig.ChatTab1Name);

        // Redo Positions
        var node = Common.GetNodeByID(unitBase, 7);
        do {
            var next = node;
            N:
            next = next->NextSiblingNode;
            if (next == null) break;
            if (next->IsVisible() == false) goto N;
            if (Math.Abs(node->Y - next->Y) >= 1) break;
            next->SetXFloat(node->X + node->GetWidth());
            node = next;
        } while (true);
    }

    public unsafe void DoRenamePanel(AtkUnitBase* panel, bool reset = false) {
        if (panel->UldManager.NodeListCount < 6) return;
        var baseComponent = (AtkComponentNode*)panel->UldManager.NodeList[5];
        if (baseComponent == null) return;
        if (baseComponent->Component->UldManager.NodeListCount < 2) return;
        var textNode = (AtkTextNode*)baseComponent->Component->UldManager.NodeList[1];
        if (textNode == null) return;

        var name = $"{(char)SeIconChar.BoxedNumber2} {((reset || !TweakConfig.DoRenameTab1 || string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) ? DefaultName1 : TweakConfig.ChatTab1Name)}";
        var str = Common.ReadSeString(textNode->NodeText.StringPtr);
        if (str.TextValue == name) return;
        SimpleLog.Log($"Rename Panel: '{str.TextValue}' -> '{name}'");

        textNode->AtkResNode.Width = 0; // Auto resizing only grows the box. Set to zero to guarantee it.
        textNode->AlignmentFontType = (byte)AlignmentType.Left; // Auto resizing doesn't work on Center aligned text.
        textNode->SetText(name);
        textNode->AtkResNode.Width += 5;
        textNode->AtkResNode.DrawFlags |= 0x1;

        baseComponent->Component->UldManager.NodeList[0]->Width = (ushort)(textNode->AtkResNode.Width + 6);
        baseComponent->Component->UldManager.NodeList[0]->DrawFlags |= 0x1;
        baseComponent->AtkResNode.Width = (ushort)(textNode->AtkResNode.Width + 6);
        baseComponent->AtkResNode.DrawFlags |= 0x1;

        panel->UldManager.NodeList[4]->X = 29 + textNode->AtkResNode.Width;
        panel->UldManager.NodeList[4]->DrawFlags |= 0x1;

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
        textNode->AtkResNode.DrawFlags |= 0x1;

        var tabWidth = (ushort)(textNode->AtkResNode.Width + 16);

        tab->Component->UldManager.NodeList[0]->Width = tabWidth;
        tab->Component->UldManager.NodeList[0]->DrawFlags |= 0x1;

        tab->Component->UldManager.NodeList[1]->Width = tabWidth;
        tab->Component->UldManager.NodeList[1]->DrawFlags |= 0x1;

        tab->Component->UldManager.NodeList[2]->Width = textNode->AtkResNode.Width;
        tab->Component->UldManager.NodeList[2]->DrawFlags |= 0x1;

        tab->AtkResNode.Width = tabWidth;
        tab->AtkResNode.DrawFlags |= 0x1;
    }
}
