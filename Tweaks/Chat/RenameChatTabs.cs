using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Rename Chat Tabs")]
[TweakDescription("Allows renaming the General and Battle tabs in the chat window.")]
[TweakAutoConfig]
[Changelog("1.10.2.0", "Fixed issue causing tabs to become incorrectly sized.")]
[Changelog("1.10.3.0", "Fixed tabs not being named on login.")]
public unsafe class RenameChatTabs : ChatTweaks.SubTweak {
    private delegate void* UpdateTabName(AgentChatLog* agent, int tabIndex, Utf8String* tabName);

    [Signature("4C 8B DC 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 83 79 20 00 49 89 5B 10")]
    private UpdateTabName updateTabName;

    public class Config : TweakConfig {
        public bool DoRenameTab0;
        public bool DoRenameTab1;
        public string ChatTab0Name = string.Empty;
        public string ChatTab1Name = string.Empty;
    }

    public Config TweakConfig { get; set; }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("###enabledRenameTab0", ref TweakConfig.DoRenameTab0);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.InputTextWithHint(LocString("TabLabel", "Tab {0}").Format(1) + "###nameTab0", defaultNames[0], ref TweakConfig.ChatTab0Name, 16);

        hasChanged |= ImGui.Checkbox("###enabledRenameTab1", ref TweakConfig.DoRenameTab1);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.InputTextWithHint(LocString("TabLabel", "Tab {0}").Format(2) + "###nameTab1", defaultNames[1], ref TweakConfig.ChatTab1Name, 16);

        if (hasChanged) Update();
    }

    private void SetName(byte tab, string name) {
        var tempName = Utf8String.FromString(name);
        RaptureLogModule.Instance()->SetTabName(tab, tempName);
        tempName->Dtor(true);
        
        var storedName = RaptureLogModule.Instance()->GetTabName(tab);
        updateTabName(AgentChatLog.Instance(), tab, storedName);
    }

    private void Update() {
        Disable();
        Enable();
    }

    protected override void Enable() {
        Service.ClientState.Login += Update;
        if (TweakConfig.DoRenameTab0 && !string.IsNullOrEmpty(TweakConfig.ChatTab0Name)) SetName(0, TweakConfig.ChatTab0Name);
        if (TweakConfig.DoRenameTab1 && !string.IsNullOrEmpty(TweakConfig.ChatTab1Name)) SetName(1, TweakConfig.ChatTab1Name);
    }

    protected override void Disable() {
        Service.ClientState.Login -= Update;
        SetName(0, defaultNames[0]);
        SetName(1, defaultNames[1]);
    }

    private readonly string[] defaultNames = [
        Service.Data.GetExcelSheet<Addon>().GetRow(662).Text.ExtractText(),
        Service.Data.GetExcelSheet<Addon>().GetRow(663).Text.ExtractText()
    ];
}
