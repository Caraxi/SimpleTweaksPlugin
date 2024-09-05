using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakAutoConfig]
[TweakName("Improved Sent Message History")]
[TweakDescription("Recover messages after accidentally pressing up in the chat, and increase the amount of history retained.")]
[TweakCategory(TweakCategory.Chat)]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class ImprovedSentMessageHistory : Tweak {
    public class Configs : TweakConfig {
        public int MaxHistory = 10;
        public bool SaveWrittenMessage = true;
    }
    
    protected void DrawConfig() {
        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt("Max messages in history", ref TweakConfig.MaxHistory, 0, 100)) {
            if (TweakConfig.MaxHistory < 0) TweakConfig.MaxHistory = 0;
        }
        
        ImGui.Checkbox("Save written message ", ref TweakConfig.SaveWrittenMessage);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Saves the current chat message to history when recalling history, preventing you from losing a message you wrote.");
    }

    public Configs TweakConfig { get; set; }

    private delegate void TrimHistory(void* a1);
    private delegate void AddHistory(RaptureAtkHistory* atkHistory, Utf8String* a2, Utf8String* a3);
    private delegate byte Previous(RaptureAtkHistory* atkHistory);
    
    [TweakHook, Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B 51 18 48 8B F9", DetourName = nameof(TrimHistoryDetour))]
    private HookWrapper<TrimHistory> trimHistoryHook;
    
    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? C6 83 ?? ?? ?? ?? ?? 4C 8B C3 C7 83 ?? ?? ?? ?? ?? ?? ?? ?? 48 8B D7", DetourName = nameof(AddHistoryDetour))]
    private HookWrapper<AddHistory> addHistoryHook;


    [TweakHook, Signature("8B 41 28 8B 51 30 FF C8", DetourName = nameof(PreviousDetour))]
    private HookWrapper<Previous> previousHook;

    private RaptureAtkHistory* ChatAtkHistory => UIModule.Instance()->AtkHistory.GetPointer(0);
    
    private void TrimHistoryDetour(void* a1) {
        if (a1 == (void*)((ulong)ChatAtkHistory + 8)) return;
        trimHistoryHook.Original(a1);
    }
    
    private void AddHistoryDetour(RaptureAtkHistory* atkHistory, Utf8String* a2, Utf8String* a3) {
        addHistoryHook.Original(atkHistory, a2, a3);
        if (atkHistory != ChatAtkHistory) return;
        var l = 0;
        while (atkHistory->Length > TweakConfig.MaxHistory && atkHistory->Length > 0 && l++ < 10) {
            trimHistoryHook.Original((void*)((ulong)atkHistory + 8));
        }
    }

    private void HandleSaveWrittenMessage(RaptureAtkHistory* atkHistory) {
        if (atkHistory->Current != -1) return;
        if (!TweakConfig.SaveWrittenMessage) return;
        if (atkHistory != ChatAtkHistory) return;

        if (!Common.GetUnitBase("ChatLog", out var chatLog)) return;
        var component = chatLog->GetComponentByNodeId(5);
        if (component == null) return;
        
        var componentInfo = (AtkUldComponentInfo*) component->UldManager.Objects;
        if (componentInfo->ComponentType != ComponentType.TextInput) return;

        var textInputComponent = (AtkComponentTextInput*)component;

        if (textInputComponent->AtkTextNode == null) return;
        
        var text = textInputComponent->AtkTextNode->GetText();
        if (text == null) return;
        
        var str = Common.ReadString(text, 500);
        if (string.IsNullOrWhiteSpace(str)) return;
        
        var a = Utf8String.FromString(str);
        UIModule.Instance()->AddAtkHistoryEntry(a, 0);
        atkHistory->Current = 0;
        a->Dtor(true);
    }
    
    private byte PreviousDetour(RaptureAtkHistory* atkHistory) {
        if (atkHistory == ChatAtkHistory) HandleSaveWrittenMessage(atkHistory);
        return previousHook.Original(atkHistory);
    }
}


