using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Rename Chat Tabs")]
[TweakDescription("Allows renaming the General and Battle tabs in the chat window.")]
[TweakAutoConfig]
[Changelog("1.10.2.0", "Fixed issue causing tabs to become incorrectly sized.")]
[Changelog(UnreleasedVersion, "Fixed tabs not being named on login.")]
public unsafe class RenameChatTabs : ChatTweaks.SubTweak {
    private delegate void SetChatTabName(RaptureLogModule* raptureLogModule, int tabIndex, Utf8String* tabName);

    private delegate Utf8String* GetChatTabName(RaptureLogModule* raptureLogModule, int tabIndex);

    private delegate void* UpdateTabName(AgentChatLog* agent, int tabIndex, Utf8String* tabName);

    [Signature("E8 ?? ?? ?? ?? 4D 8B 44 24 ?? 41 8B D7")]
    private SetChatTabName setChatTabName;

    [Signature("E8 ?? ?? ?? ?? 44 8D 73 01")]
    private GetChatTabName getChatTabName;

    [Signature("E8 ?? ?? ?? ?? 48 8B 8D ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 41 5F 41 5D 41 5C 5F")]
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
        setChatTabName(RaptureLogModule.Instance(), tab, tempName);
        tempName->Dtor(true);

        var storedName = getChatTabName(RaptureLogModule.Instance(), tab);
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
        Service.Data.GetExcelSheet<Addon>()?.GetRow(662)?.Text.RawString ?? "General",
        Service.Data.GetExcelSheet<Addon>()?.GetRow(663)?.Text.RawString ?? "Battle"
    ];
}
