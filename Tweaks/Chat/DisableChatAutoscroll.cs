using System.Runtime.InteropServices;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Smart AutoScroll")]
[TweakDescription("Attempts to prevent autoscrolling when receiving new chat messages while scrolled up.")]
[TweakAutoConfig]
public unsafe class DisableChatAutoscroll : ChatTweaks.SubTweak {
    public class Configs : TweakConfig {
        public PanelConfig Panel0 = new();
        public PanelConfig Panel1 = new();
        public PanelConfig Panel2 = new();
        public PanelConfig Panel3 = new();
    }

    public class PanelConfig {
        public bool Enabled = true;
        public int Tolerance;
    }

    public Configs Config { get; private set; }

    private void DrawPanelConfig(string name, ref bool hasChanged, PanelConfig panelConfig) {
        using var _ = ImRaii.PushId(name);

        hasChanged |= ImGui.Checkbox($"Disable Autoscroll in {name}", ref panelConfig.Enabled);

        if (panelConfig.Enabled) {
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            ImGui.Indent();
            hasChanged |= ImGui.InputInt("Minimum messages below scroll", ref panelConfig.Tolerance);
            ImGui.Unindent();
        }

        ImGui.Spacing();
    }

    protected void DrawConfig(ref bool hasChanged) {
        DrawPanelConfig("Tab 1", ref hasChanged, Config.Panel0);
        DrawPanelConfig("Tab 2", ref hasChanged, Config.Panel1);
        DrawPanelConfig("Tab 3", ref hasChanged, Config.Panel2);
        DrawPanelConfig("Tab 4", ref hasChanged, Config.Panel3);
    }

    private delegate void* ScrollToBottomDelegate(LogViewer* a1);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B 43 10 33 D2", DetourName = nameof(ScrollToBottomDetour))]
    private HookWrapper<ScrollToBottomDelegate> scrollToBottomHook;

    [StructLayout(LayoutKind.Explicit, Size = 0x128)]
    public struct LogViewer {
        [FieldOffset(0x10)] public AddonChatLogPanel* ChatLogPanel;
        [FieldOffset(0x18)] public AtkTextNode* ChatText;
        [FieldOffset(0x48)] public byte FontSize;
        [FieldOffset(0x4C)] public uint FirstLineVisible;
        [FieldOffset(0x50)] public uint LastLineVisible;
        [FieldOffset(0x58)] public uint Unknown2C0;
        [FieldOffset(0x5C)] public uint TotalLineCount;
        [FieldOffset(0x90)] public uint MessagesAboveCurrent;
        [FieldOffset(0xD9)] public byte IsScrolledBottom;
    }

    private bool PreventScroll(LogViewer* logViewer) {
        if (logViewer == null || logViewer->ChatLogPanel == null) return false; // Sanity
        var panelName = logViewer->ChatLogPanel->NameString;
        var config = panelName switch {
            "ChatLogPanel_0" => Config.Panel0,
            "ChatLogPanel_1" => Config.Panel1,
            "ChatLogPanel_2" => Config.Panel2,
            "ChatLogPanel_3" => Config.Panel3,
            _ => null
        };
        if (config == null) {
            SimpleLog.Verbose($"Unknown panel {panelName} - Allow scroll");
            return false;
        }

        if (!config.Enabled) return false;
        if (logViewer->TotalLineCount <= logViewer->LastLineVisible || logViewer->TotalLineCount == uint.MaxValue) return false;
        if (config.Tolerance <= 0) return true;
        var messagesBelow = logViewer->TotalLineCount - logViewer->LastLineVisible;
        return messagesBelow >= config.Tolerance;
    }

    private void* ScrollToBottomDetour(LogViewer* a1) {
        try {
            if (PreventScroll(a1)) return null;
            return scrollToBottomHook.Original(a1);
        } catch {
            return scrollToBottomHook.Original(a1);
        }
    }
}
