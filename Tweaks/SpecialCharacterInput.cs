using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Special Character Input")]
[TweakDescription("Adds a window for adding special characters to text inputs.")]
[TweakAutoConfig]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class SpecialCharacterInput : Tweak {
    private readonly ushort[] additionalSeIconChar = [0xE032, 0xE038, 0xE039, 0xE03F, 0xE044, 0xE05A, 0xE05E, 0xE05F, 0xE0B0];  // SeIconChar without name.
    
    protected override void ConfigChanged() {
        // Create Sets
        sets.Clear();
        setSizes.Clear();
        if (Config.ShowHistory) sets.Add($"{(char)SeIconChar.Clock}{(Config.ShowTitles ? " History" : "")}", new Lazy<IEnumerable<string>>(() => Config.History));
        sets.Add($"{(char)SeIconChar.BotanistSprout}{(Config.ShowTitles ? " Custom" : "")}", new Lazy<IEnumerable<string>>(() => Config.Custom));
        sets.Add($"{(char)SeIconChar.BoxedStar}{(Config.ShowTitles ? " FFXIV" : "")}", new Lazy<IEnumerable<string>>(() => Enum.GetValues<SeIconChar>()
                .Select(i => i.ToIconString())
                .Concat(additionalSeIconChar.Select(s => $"{(char)s}")).OrderBy(s => s).Distinct()));
        sets.Add($"♡{(Config.ShowTitles ? " Other" : "")}", new Lazy<IEnumerable<string>>(() => "←→↑↓《》■※☀★★☆♥♡☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«»".ToCharArray().Select(c => c.ToString())));
    }

    public class TweakConfigs : TweakConfig {
        public VirtualKey[] ToggleHotkey = [VirtualKey.MENU, VirtualKey.S];
        public List<string> History = [];
        public List<string> Custom = [];
        public bool ShowHistory = false;
        public bool ShowAllTab = false;
        public int MaxHistory = 25;
        public bool ShowTitles = true;
    }

    [TweakConfig] public TweakConfigs Config { get; private set; } = new();
    
    private readonly Dictionary<string, Lazy<IEnumerable<string>>> sets = new();
    private readonly Dictionary<string, float> setSizes = new();

    protected void DrawConfig(ref bool hasChanged) {
        if (HotkeyHelper.DrawHotkeyConfigEditor("Toggle Character Selector", Config.ToggleHotkey, out var newKeys)) {
            Config.ToggleHotkey = newKeys;
            hasChanged = true;
        }
        
        hasChanged |= ImGui.Checkbox("Show Full Tab Titles", ref Config.ShowTitles);
        hasChanged |= ImGui.Checkbox("Show All Tab", ref Config.ShowAllTab);

        hasChanged |= ImGui.Checkbox("Show History", ref Config.ShowHistory);

        using (ImRaii.Disabled(!Config.ShowHistory)) {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear History")) {
                Config.History.Clear();
            }

            using (ImRaii.PushIndent()) {
                ImGui.SetNextItemWidth(130 * ImGuiHelpers.GlobalScale);
                hasChanged |= ImGui.InputInt("Max History", ref Config.MaxHistory);
            }
        }

        if (ImGui.CollapsingHeader($"Custom Entries ({Config.Custom.Count})###customEntriesHeader")) {
            var delete = -1;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(1, ImGui.GetStyle().ItemSpacing.Y))) {
                int i;
                for (i = 0; i < Config.Custom.Count; i++) {
                    if (ImGui.Button($"{(char)SeIconChar.Cross}##{i}")) {
                        delete = i;
                    }

                    ImGui.SameLine();
                    var v = Config.Custom[i];
                    if (ImGui.InputText($"##custom_{i}", ref v, 64)) {
                        Config.Custom[i] = v;
                        hasChanged = true;
                    }

                    if (string.IsNullOrWhiteSpace(v) && !ImGui.IsItemActive()) {
                        delete = i;
                    }
                }

                using (ImRaii.Disabled()) {
                    ImGui.Button($"{(char)SeIconChar.GlamouredDyed}##{i}");
                }

                ImGui.SameLine();
                var newText = string.Empty;
                if (ImGui.InputText($"##custom_{i}", ref newText, 32)) {
                    Config.Custom.Add(newText);
                    hasChanged = true;
                }
            }

            if (delete == -1) return;
            
            Config.Custom.RemoveAt(delete);
            hasChanged = true;
        }
    }

    private AtkComponentTextInputExt* GetFocusedTextInput() {
        var atkStage = AtkStage.Instance();
        if (atkStage == null) return null;
        var focus = atkStage->GetFocus();
        if (focus == null) return null;
        var focusParent = focus->ParentNode;
        if (focusParent == null) return null;
        var focusParentComponent = focusParent->GetComponent();
        if (focusParentComponent == null) return null;
        var componentInfo = (AtkUldComponentInfo*)focusParentComponent->UldManager.Objects;
        if (componentInfo == null || componentInfo->ComponentType != ComponentType.TextInput) return null;
        return (AtkComponentTextInputExt*)focusParentComponent;
    }

    protected override void AfterEnable() {
        ConfigChanged();
        PluginInterface.UiBuilder.Draw += DrawWindow;
    }

    private AtkComponentTextInputExt* windowEnabledFor;

    [FrameworkUpdate]
    private void FrameworkUpdate() {
        var focusedTextInput = GetFocusedTextInput();
        if (focusedTextInput == null) {
            windowEnabledFor = null;
            return;
        }

        if (HotkeyHelper.CheckHotkeyState(Config.ToggleHotkey)) {
            windowEnabledFor = windowEnabledFor != focusedTextInput ? focusedTextInput : null;
            return;
        }

        if (windowEnabledFor != focusedTextInput) {
            windowEnabledFor = null;
        }
    }

    private ulong id;

    private void DrawWindow() {
        id = 0;
        var focusedTextInput = GetFocusedTextInput();

        if (windowEnabledFor != focusedTextInput || focusedTextInput == null) {
            return;
        }

        ImGui.SetNextWindowSizeConstraints(new Vector2(200, 200), new Vector2(float.MaxValue, float.MaxValue));
        if (!ImGui.Begin($"###SimpleTweaks_{nameof(SpecialCharacterInput)}", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;
        
        try {
            DrawWindowContents(focusedTextInput);
        } catch (Exception e) {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed)) {
                ImGui.TextUnformatted(e.ToString());
            }
        } finally {
            
            ImGui.End();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = AtkComponentTextInput.StructSize)]
    private struct AtkComponentTextInputExt {
        [FieldOffset(0x00)] public AtkComponentTextInput AtkComponentTextInput;
        [FieldOffset(0xD0)] public AtkResNode* CursorContainer;
        [FieldOffset(0x158)] public int Length;
        [FieldOffset(0x1C4)] public int P1;
        [FieldOffset(0x1C8)] public int P2;
        [FieldOffset(0x1CC)] public int P3;
        [FieldOffset(0x272)] public ushort Unk272;
    }

    private void InsertString(AtkComponentTextInputExt* textInput, string text, bool doHistory) {
        if (doHistory) {
            Config.History.Remove(text);
            Config.History.Insert(0, text);
            setSizes.Remove($"{(char)SeIconChar.Clock}");
            if (Config.History.Count > Config.MaxHistory) {
                Config.History.RemoveRange(Config.MaxHistory, Config.History.Count - Config.MaxHistory);
            }
        }

        textInput->P1 += text.Length;
        textInput->P2 = textInput->P1;
        textInput->P3 = textInput->P1;

        textInput->AtkComponentTextInput.InsertText(text);
    }

    private void ShowSet(string set, IEnumerable<string> options, AtkComponentTextInputExt* textInput) {
        var optionArray = options.ToArray();
        if (!setSizes.TryGetValue(set, out var btnWidth)) {
            btnWidth = optionArray.Max(s => ImGui.CalcTextSize(s)
                .X);
            setSizes.Add(set, btnWidth);
        }

        var buttonWidthWithPadding = btnWidth + ((ImGui.GetStyle()
            .FramePadding.X * 2 + ImGui.GetStyle()
            .ItemSpacing.X) * ImGuiHelpers.GlobalScale);
        var buttonsPerRow = MathF.Floor((ImGui.GetContentRegionAvail()
            .X - ImGui.GetStyle()
            .ItemSpacing.X) / buttonWidthWithPadding);
        if (buttonsPerRow == 0) buttonsPerRow = 1;

        var buttonSize = new Vector2(ImGui.GetContentRegionAvail()
            .X / buttonsPerRow, 32 * ImGuiHelpers.GlobalScale);

        var c = 0;
        for (var i = 0; i < optionArray.Length; i++) {
            var e = optionArray[i];
            if (ImGui.Button(e + $"##button_{id++}", buttonSize - ImGui.GetStyle()
                    .ItemSpacing.X * Vector2.UnitX)) {
                InsertString(textInput, e, !object.ReferenceEquals(options, Config.History));
            }

            if (c++ < buttonsPerRow - 1 && i != optionArray.Length - 1) {
                ImGui.SameLine();
            } else {
                c = 0;
            }
        }
    }

    private void DrawWindowContents(AtkComponentTextInputExt* textInput) {
        using var tabBar = ImRaii.TabBar("IconSets");
        if (!tabBar) return;

        if (Config.ShowAllTab) {
            using var allTab = ImRaii.TabItem($"All##tab");
            if (allTab) {
                using var child = ImRaii.Child("all_scroll", ImGui.GetContentRegionAvail());
                if (child) {
                    foreach (var set in sets) {
                        if (!set.Value.Value.Any()) continue;
                        if (ImGui.CollapsingHeader(set.Key + $"##header", ImGuiTreeNodeFlags.DefaultOpen)) {
                            ShowSet(set.Key, set.Value.Value, textInput);
                        }
                    }
                }
            }
        }

        foreach (var set in sets) {
            if (!set.Value.Value.Any()) continue;
            using var tabItem = ImRaii.TabItem($"{set.Key}##tab");
            if (!tabItem) continue;
            using var child = ImRaii.Child($"{set.Key}_scroll", ImGui.GetContentRegionAvail());
            if (child) {
                ShowSet(set.Key, set.Value.Value, textInput);
            }
        }
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= DrawWindow;
    }
}
