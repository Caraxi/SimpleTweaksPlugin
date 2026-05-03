using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Special Character Input")]
[TweakDescription("Adds a window for adding special characters to text inputs")]
[TweakAuthor("Bryer")]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.6.0")]
public sealed unsafe class SpecialCharacterInput : ChatTweaks.SubTweak
{
    private const string ChatLogAddonName = "ChatLog";
    private static readonly string[] RecruitmentCriteriaAddonNames =
    [
        "LookingForGroupCondition",
        "LookingForGroup",
        "LookingForGroupDetail",
        "LookingForGroupSearch",
        "LookingForGroupSelectRole",
    ];

    private static readonly string[] MessageBookInputAddonNames =
    [
        "InputMessage",
        "HousingGuestBook",
        "HousingGuestBookInputMessage",
    ];

    private const int MaxColumns = 10;
    private static readonly string[] Symbols = BuildSymbols();

    public class TweakConfigs : TweakConfig
    {
        public VirtualKey[] ToggleHotkey = [VirtualKey.MENU, VirtualKey.S];
        public List<string> Custom = [];
        public List<string> favsymbols = [];

        // Keep this in sake of compatibility with older 'SpecialCharacterInput' config files
        public List<string> History = [];
        public bool ShowHistory = false;
        public bool ShowAllTab = false;
        public bool ShowTitles = true;
        // #
        public int MaxHistory = 25;

        // Kept from my original 'QuickSymbols' behaviour so the button can be moved and continue to follow the native ChatLog placement
        public bool HasCustombPosition { get; set; }
        public bool UsesRelativeButtonOffset { get; set; }
        public Vector2 bPosition { get; set; }
        public Vector2 ButtonOffset { get; set; }
    }

    [TweakConfig]
    public TweakConfigs Config { get; private set; } = new();

    // UI and State stuff
    private IFontHandle? symbolFont;
    private PopupTab selectedPopupTab = PopupTab.Symbols;
    private string newCustomEntry = string.Empty;
    // #

    // Control and Visibility stuff
    private bool popupOpen;
    private bool partyFinderPopupOpen;
    private bool messageBookPopupOpen;
    private bool keybindPopupOpen;
    // #

    // Main positioning related stuff
    private bool editbPosition;
    private bool draggingButton;
    private bool bPositionDirty;
    private Vector2 nativebPos;
    private Vector2 currentbPos;
    private Vector2 currentbSize;
    // #

    // Party Finder/House Message Book
    private Vector2 partyFinderbPos;
    private Vector2 partyFinderbSize;
    private Vector2 messageBookbPos;
    private Vector2 messageBookbSize;
    // #

    // New Keybind Popup | Kept 'Toggle Character Selector' option from original 'SpecialCharacterInput'
    private Vector2 keybindPopupAnchorPos;
    private Vector2 keybindPopupAnchorSize;
    private AtkComponentTextInput* keybindTextInput;
    // #

    // Scroll
    private float symbolScrollY;
    private float customScrollY;
    private bool draggingScrollBar;
    private float scrollDragOffsetY;
    // #

    protected override void Setup()
    {
        this.Config = this.LoadConfig<TweakConfigs>() ?? new TweakConfigs();
        this.ConfigChanged();
    }

    protected override void ConfigChanged()
    {
        // Ensures that lists do not become empty after loading
        this.Config.Custom ??= [];
        this.Config.favsymbols ??= [];
        this.Config.History ??= [];
    }

    protected override void Enable()
    {
        // Using Axis '18pt' to make the symbols more legible
        this.symbolFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 18f));
        PluginInterface.UiBuilder.Draw += this.Draw;
    }

    protected override void Disable()
    {
        PluginInterface.UiBuilder.Draw -= this.Draw;

        this.symbolFont?.Dispose();
        this.symbolFont = null;

        this.popupOpen = partyFinderPopupOpen = messageBookPopupOpen = keybindPopupOpen = false;
        this.keybindTextInput = null;
        this.editbPosition = draggingButton = draggingScrollBar = false;

        this.SaveConfig(this.Config);
    }

    public override void Dispose()
    {
        this.symbolFont?.Dispose();
        this.symbolFont = null;
        base.Dispose();
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        this.ConfigChanged();

        if (HotkeyHelper.DrawHotkeyConfigEditor("Toggle Character Selector", this.Config.ToggleHotkey, out var newKeys))
        {
            this.Config.ToggleHotkey = newKeys;
            hasChanged = true;
        }

        ImGui.Spacing();
        if (ImGui.CollapsingHeader($"Custom Entries ({this.Config.Custom.Count})###cEntriesHeader", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var delete = -1;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4f * ImGuiHelpers.GlobalScale, ImGui.GetStyle().ItemSpacing.Y)))
            {
                for (var i = 0; i < this.Config.Custom.Count; i++)
                {
                    if (ImGui.SmallButton($"{(char)SeIconChar.Cross}##deleteCustom{i}"))
                    {
                        delete = i;
                    }

                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(Math.Max(180f, ImGui.GetContentRegionAvail().X));
                    var val = this.Config.Custom[i];
                    if (ImGui.InputText($"##custom_{i}", ref val, 128))
                    {
                        this.Config.Custom[i] = val;
                        hasChanged = true;
                    }

                    if (string.IsNullOrWhiteSpace(val) && !ImGui.IsItemActive())
                    {
                        delete = i;
                    }
                }

                if (delete >= 0)
                {
                    this.Config.Custom.RemoveAt(delete);
                    hasChanged = true;
                }

                ImGui.Separator();
                ImGui.SetNextItemWidth(Math.Max(180f, ImGui.GetContentRegionAvail().X - 76f * ImGuiHelpers.GlobalScale));
                ImGui.InputText("##newCustomEntry", ref this.newCustomEntry, 128);
                ImGui.SameLine();
                if (ImGui.SmallButton("+ Add##addCustomEntry"))
                {
                    var entry = this.newCustomEntry.Trim();
                    if (!string.IsNullOrWhiteSpace(entry))
                    {
                        this.Config.Custom.Add(entry);
                        this.newCustomEntry = string.Empty;
                        hasChanged = true;
                    }
                }
            }
        }
    }

    [FrameworkUpdate]
    private void FrameworkUpdate()
    {
        if (!HotkeyHelper.CheckHotkeyState(this.Config.ToggleHotkey))
        {
            return;
        }

        if (this.keybindPopupOpen)
        {
            this.keybindPopupOpen = false;
            this.keybindTextInput = null;
            return;
        }

        var focused = GetFocusedTextInput();
        if (focused == null)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        this.keybindTextInput = focused;
        this.keybindPopupAnchorSize = new Vector2(Math.Clamp(24f * scale, 18f * scale, 28f * scale));
        this.keybindPopupAnchorPos = ClampPositionToScreen(ImGui.GetIO().MousePos + new Vector2(12f * scale, 12f * scale), this.keybindPopupAnchorSize);
        this.selectedPopupTab = PopupTab.Symbols;
        this.keybindPopupOpen = true;
    }

    private static AtkComponentTextInput* GetFocusedTextInput()
    {
        var atkStage = AtkStage.Instance();
        if (atkStage == null)
        {
            return null;
        }

        var focus = atkStage->GetFocus();
        var node = focus;
        // Try to move up the hierarchy to find the input
        for (var i = 0; node != null && i < 8; i++)
        {
            var input = node->GetAsAtkComponentTextInput();
            if (input != null && input->Enabled)
            {
                return input;
            }

            node = node->ParentNode;
        }

        if (focus == null || focus->ParentNode == null)
        {
            return null;
        }

        var focusParentComponent = focus->ParentNode->GetComponent();
        if (focusParentComponent == null)
        {
            return null;
        }

        var componentInfo = (AtkUldComponentInfo*)focusParentComponent->UldManager.Objects;
        if (componentInfo == null || componentInfo->ComponentType != ComponentType.TextInput)
        {
            return null;
        }

        return (AtkComponentTextInput*)focusParentComponent;
    }

    private void Draw()
    {
        if (Service.GameGui.GameUiHidden)
        {
            return;
        }

        var Theme = GetCurrentGameUiTheme();
        var colors = UiColors.FromGameTheme(Theme, null);
        var ChatPopup = false;
        var PartyFinderPopup = false;
        var MsgBookPopup = false;

        // Chat Log button
        if (this.TryGetNativeChatButtonPlacement(out var nPos, out var nSize, out colors))
        {
            this.nativebPos = nPos;
            this.currentbSize = nSize;
            this.currentbPos = this.GetCurrentbPosition(nPos, nSize);

            this.DrawChatButton(this.currentbPos, nSize, colors);
            ChatPopup = this.popupOpen;
        }
        else
        {
            this.popupOpen = false;
            this.editbPosition = false;
        }
        // Party Finder button
        if (this.TryGetRecruitmentCommentTarget(out var pfTarget))
        {
            var scale = ImGuiHelpers.GlobalScale;
            var refSide = this.currentbSize.Y > 0.1f ? this.currentbSize.Y : 24f * scale;
            var Side = Math.Clamp(Math.Min(refSide, pfTarget.Size.Y * 0.50f), 18f * scale, 28f * scale);
            this.partyFinderbSize = new Vector2(Side, Side);
            this.partyFinderbPos = ClampPositionToScreen(
                new Vector2(pfTarget.Position.X + 6f * scale, pfTarget.Position.Y + pfTarget.Size.Y + 2f * scale),
                this.partyFinderbSize);

            this.DrawContextButton(
                "##SpecialCharacterInputRecruitmentCommentButtonOverlay",
                "##SpecialCharacterInputRecruitmentCommentOpenButton",
                this.partyFinderbPos,
                this.partyFinderbSize,
                colors,
                ref this.partyFinderPopupOpen);
            PartyFinderPopup = this.partyFinderPopupOpen;
        }
        else
        {
            this.partyFinderPopupOpen = false;
        }
        // Guestbook/Message Book button
        if (this.TryGetMessageBookInputTarget(out var messageTarget))
        {
            var scale = ImGuiHelpers.GlobalScale;
            var refSide = this.currentbSize.Y > 0.1f ? this.currentbSize.Y : 24f * scale;
            var Side = Math.Clamp(Math.Min(refSide, messageTarget.Size.Y * 0.58f), 18f * scale, 28f * scale);
            this.messageBookbSize = new Vector2(Side, Side);
            this.messageBookbPos = ClampPositionToScreen(
                new Vector2(messageTarget.Position.X + 6f * scale, messageTarget.Position.Y + messageTarget.Size.Y + 3f * scale),
                this.messageBookbSize);

            this.DrawContextButton(
                "##SpecialCharacterInputMessageBookButtonOverlay",
                "##SpecialCharacterInputMessageBookOpenButton",
                this.messageBookbPos,
                this.messageBookbSize,
                colors,
                ref this.messageBookPopupOpen);
            MsgBookPopup = this.messageBookPopupOpen;
        }
        else
        {
            this.messageBookPopupOpen = false;
        }
        // Popup Rendering
        if (ChatPopup)
        {
            this.DrawSymbolsPopup(
                "Chat",
                colors,
                this.currentbPos,
                this.currentbSize,
                PopupPlacement.AboveRight, includePositionEditor: true, SymbolInsertTarget.Chat, ref this.popupOpen);
        }

        if (PartyFinderPopup)
        {
            this.DrawSymbolsPopup(
                "PartyFinder",
                colors,
                this.partyFinderbPos,
                this.partyFinderbSize,
                PopupPlacement.Below, includePositionEditor: false, SymbolInsertTarget.RecruitmentComment, ref this.partyFinderPopupOpen);
        }

        if (MsgBookPopup)
        {
            this.DrawSymbolsPopup(
                "MessageBook",
                colors,
                this.messageBookbPos,
                this.messageBookbSize, PopupPlacement.Below, includePositionEditor: false, SymbolInsertTarget.MessageBookInput, ref this.messageBookPopupOpen);
        }

        if (this.keybindPopupOpen)
        {
            this.DrawSymbolsPopup(
                "Keybind",
                colors,
                this.keybindPopupAnchorPos,
                this.keybindPopupAnchorSize, PopupPlacement.Below, includePositionEditor: false, SymbolInsertTarget.FocusedTextInput, ref this.keybindPopupOpen);
        }
    }

    private Vector2 GetCurrentbPosition(Vector2 nPos, Vector2 nSize)
    {
        if (!this.Config.HasCustombPosition)
        {
            return nPos;
        }

        if (!this.Config.UsesRelativeButtonOffset)
        {
            this.Config.ButtonOffset = this.Config.bPosition - nPos;
            this.Config.UsesRelativeButtonOffset = true;
            this.bPositionDirty = true;
        }

        var desired = nPos + this.Config.ButtonOffset;
        var clamped = ClampPositionToScreen(desired, nSize);

        if (Vector2.DistanceSquared(desired, clamped) > 0.01f)
        {
            this.Config.ButtonOffset = clamped - nPos;
            this.Config.bPosition = clamped;
            this.bPositionDirty = true;
        }

        this.SaveConfigurationIfDirty();
        return clamped;
    }

    private bool TryGetNativeChatButtonPlacement(out Vector2 bPos, out Vector2 bSize, out UiColors colors)
    {
        bPos = Vector2.Zero;
        bSize = Vector2.Zero;
        colors = UiColors.Default;

        var chatUnit = Service.GameGui.GetAddonByName(ChatLogAddonName);
        if (chatUnit.IsNull || !chatUnit.IsReady || !chatUnit.IsVisible)
        {
            return false;
        }

        var chatLog = Service.GameGui.GetAddonByName<AddonChatLog>(ChatLogAddonName);
        if (chatLog == null)
        {
            return false;
        }

        colors = UiColors.FromGameTheme(GetCurrentGameUiTheme(), chatLog);

        var scale = Math.Clamp(chatUnit.Scale, 0.65f, 2.4f);
        var gap = Math.Max(2f, 2f * scale);

        //Try to find the channel dropdown
        if (chatLog->ChannelSelectDropDown != null)
        {
            var node = chatLog->ChannelSelectDropDown->AtkComponentBase.OwnerNode;
            if (node != null && node->AtkResNode.IsVisible())
            {
                var res = &node->AtkResNode;
                var nodeHeight = GetNodeScreenSize(res, scale).Y;
                var sq = Math.Clamp(nodeHeight, 18f * scale, 28f * scale);

                bSize = new Vector2(sq, sq);
                bPos = new Vector2(
                    res->ScreenX - sq - gap,
                    res->ScreenY + Math.Max(0f, (nodeHeight - sq) * 0.5f));
                return true;
            }
        }

        if (chatLog->CurrentChannelTextNode != null && chatLog->CurrentChannelTextNode->AtkResNode.IsVisible())
        {
            var res = &chatLog->CurrentChannelTextNode->AtkResNode;
            var h = GetNodeScreenSize(res, scale).Y;
            var sq = Math.Clamp(h + 8f * scale, 18f * scale, 28f * scale);

            bSize = new Vector2(sq, sq);
            bPos = new Vector2(
                res->ScreenX - sq - gap,
                res->ScreenY - 4f * scale);
            return true;
        }

        // Last resort: bottom left corner
        var fSize = Math.Clamp(24f * scale, 18f, 32f * scale);
        bSize = new Vector2(fSize, fSize);
        bPos = new Vector2(
            chatUnit.Position.X + 4f * scale,
            chatUnit.Position.Y + chatUnit.ScaledSize.Y - fSize - 4f * scale);
        return true;
    }

    private void DrawChatButton(Vector2 position, Vector2 size, UiColors colors)
    {
        var clicked = this.DrawHeartButtonOverlay(
            "##SpecialCharacterInputChatButtonOverlay",
            "##SpecialCharacterInputOpenButton", position, size, colors,
            this.editbPosition, out var active);

        if (this.editbPosition)
        {
            this.HandleButtonDragging(size, active);
        }
        else if (clicked)
        {
            this.popupOpen = !this.popupOpen;
        }
    }

    private void DrawContextButton(string windowId, string buttonId, Vector2 position, Vector2 size, UiColors colors, ref bool isOpen)
    {
        if (this.DrawHeartButtonOverlay(windowId, buttonId, position, size, colors, editing: false, out _))
        {
            isOpen = !isOpen;
        }
    }

    private bool DrawHeartButtonOverlay(string windowId, string buttonId, Vector2 position, Vector2 size, UiColors colors, bool editing, out bool active)
    {
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoNav;

        var clicked = false;
        active = false;
        var beginCalled = false;

        using var wPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        using var wBorder = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        using var wRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 0f);
        using var wBackground = ImRaii.PushColor(ImGuiCol.WindowBg, Vector4.Zero);

        try
        {
            var wVisible = ImGui.Begin(windowId, flags);
            beginCalled = true;
            if (wVisible)
            {
                var drawList = ImGui.GetWindowDrawList();
                var min = ImGui.GetWindowPos();
                var max = min + size;

                ImGui.SetCursorScreenPos(min);
                clicked = ImGui.InvisibleButton(buttonId, size);
                var hovered = ImGui.IsItemHovered();
                active = ImGui.IsItemActive();

                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                var background = editing
                    ? colors.EditButton
                    : active
                        ? colors.ButtonActive
                        : colors.Button;

                var rounding = Math.Max(2f, size.Y * 0.14f);
                drawList.AddRectFilled(min, max, Color(background), rounding);
                drawList.AddRect(min, max, Color(colors.Border), rounding, ImDrawFlags.None, Math.Max(1f, size.Y * 0.045f));

                IDisposable? pushedFont = null;
                try
                {
                    if (this.symbolFont is { Available: true })
                    {
                        pushedFont = this.symbolFont.Push();
                    }

                    var text = "♥";
                    var textSize = ImGui.CalcTextSize(text);
                    var textPos = min + (size - textSize) * 0.5f;
                    var textColor = hovered && !editing
                        ? new Vector4(1f, 0.08f, 0.08f, 1f)
                        : colors.Text;

                    drawList.AddText(textPos, Color(textColor), text);
                }
                finally
                {
                    pushedFont?.Dispose();
                }
            }
        }
        finally
        {
            if (beginCalled)
            {
                ImGui.End();
            }
        }

        return clicked;
    }

    private void HandleButtonDragging(Vector2 size, bool active)
    {
        var io = ImGui.GetIO();
        if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            this.draggingButton = true;
            var clamped = ClampPositionToScreen(this.currentbPos + io.MouseDelta, size);
            this.Config.HasCustombPosition = true;
            this.Config.UsesRelativeButtonOffset = true;
            this.Config.bPosition = clamped;
            this.Config.ButtonOffset = clamped - this.nativebPos;
            this.currentbPos = clamped;
            this.bPositionDirty = true;
        }
        else if (this.draggingButton && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            this.draggingButton = false;
            this.SaveConfigurationIfDirty();
        }
    }

    private void DrawSymbolsPopup(
        string idSuffix, UiColors colors, Vector2 anchorPos, Vector2 anchorSize, PopupPlacement placement,
        bool includePositionEditor, SymbolInsertTarget insertTarget, ref bool isOpen)
    {
        this.ConfigChanged();

        var cEntries = this.GetcEntries();
        var scale = ImGuiHelpers.GlobalScale;
        var dSize = ImGui.GetIO().DisplaySize;
        var cell = Math.Clamp(anchorSize.Y * 1.05f, 22f * scale, 34f * scale);
        var spacing = Math.Max(3f, 4f * scale);
        var padding = Math.Max(8f, 10f * scale);
        var scrollWidth = Math.Max(3f, 4f * scale);
        var availableWidth = dSize.X - 16f * scale;

        // Grid Calc | Keep the popup dimensions tied to the normal Symbols tab. Custom entries can scroll inside the same space but they should never resize the window
        var columns = Math.Clamp((int)((availableWidth - padding * 2f - scrollWidth - 8f * scale + spacing) / (cell + spacing)), 1, MaxColumns);
        var sRows = Math.Max(1, (int)Math.Ceiling(Symbols.Length / (double)columns));
        var visibleRows = Math.Min(sRows, 8);
        var gridWidth = columns * cell + Math.Max(0, columns - 1) * spacing;
        var gridHeight = visibleRows * cell + Math.Max(0, visibleRows - 1) * spacing;
        var headerHeight = 24f * scale;
        var tabHeight = 22f * scale;
        var contentGap = 3f * scale;
        var pWidth = Math.Min(availableWidth, padding * 2f + gridWidth + scrollWidth + 8f * scale);
        var pHeight = padding * 2f + headerHeight + tabHeight + contentGap + gridHeight;

        float posX;
        float posY;
        if (placement == PopupPlacement.Below)
        {
            posX = anchorPos.X;
            posY = anchorPos.Y + anchorSize.Y + 6f * scale;
        }
        else
        {
            posX = anchorPos.X + anchorSize.X + 10f * scale;
            posY = anchorPos.Y - pHeight - 8f * scale;
        }

        posX = Math.Clamp(posX, 8f * scale, Math.Max(8f * scale, dSize.X - pWidth - 8f * scale));
        posY = Math.Clamp(posY, 8f * scale, Math.Max(8f * scale, dSize.Y - pHeight - 8f * scale));

        ImGui.SetNextWindowPos(new Vector2(posX, posY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(pWidth, pHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoNav;

        var beginCalled = false;

        using var pPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(padding, padding));
        using var pBorderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f * scale);
        using var pRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 8f * scale);
        using var pBackground = ImRaii.PushColor(ImGuiCol.WindowBg, colors.PopupBackground);
        using var pBorder = ImRaii.PushColor(ImGuiCol.Border, colors.Border);

        try
        {
            var windowVisible = ImGui.Begin($"##SpecialCharacterInputPopup{idSuffix}", flags);
            beginCalled = true;
            if (windowVisible)
            {
                var wPos = ImGui.GetWindowPos();
                var drawList = ImGui.GetWindowDrawList();
                var title = "Bryer - Special Character Input";
                ImGui.TextColored(colors.MutedText, title);

                var closeSize = new Vector2(22f * scale, 22f * scale);
                var closePos = new Vector2(wPos.X + pWidth - padding - closeSize.X, wPos.Y + padding - 1f * scale);

                if (includePositionEditor)
                {
                    var editLabel = this.editbPosition ? "Editing button position" : "Change button position";
                    var editbSize = new Vector2(
                        Math.Min(
                            Math.Max(126f * scale, ImGui.CalcTextSize(editLabel).X + 16f * scale),
                            Math.Max(80f * scale, closePos.X - (wPos.X + padding + ImGui.CalcTextSize(title).X + 12f * scale) - 6f * scale)),
                        22f * scale);
                    var editbPos = new Vector2(wPos.X + padding + ImGui.CalcTextSize(title).X + 12f * scale, wPos.Y + padding - 1f * scale);

                    ImGui.SetCursorScreenPos(editbPos);
                    using (ImRaii.PushColor(ImGuiCol.Button, this.editbPosition ? colors.EditButton : colors.Button))
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, this.editbPosition ? colors.EditButtonHovered : colors.ButtonHovered))
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, colors.ButtonActive))
                    using (ImRaii.PushColor(ImGuiCol.Text, colors.Text))
                    {
                        if (ImGui.Button(editLabel, editbSize))
                        {
                            this.editbPosition = !this.editbPosition;
                            this.draggingButton = false;
                            this.SaveConfigurationIfDirty();
                        }
                    }
                }

                ImGui.SetCursorScreenPos(closePos);
                if (ImGui.InvisibleButton($"##SpecialCharacterInputCloseButton{idSuffix}", closeSize))
                {
                    isOpen = false;
                    if (idSuffix == "Keybind")
                    {
                        this.keybindTextInput = null;
                    }
                }

                var cHover = ImGui.IsItemHovered();
                drawList.AddRectFilled(closePos, closePos + closeSize, Color(cHover ? colors.CellHovered : colors.CellBackground), 4f * scale);
                var xText = "X";
                var xSize = ImGui.CalcTextSize(xText);
                drawList.AddText(closePos + (closeSize - xSize) * 0.5f, Color(colors.Text), xText);

                var tabStartY = wPos.Y + padding + headerHeight;
                ImGui.SetCursorScreenPos(new Vector2(wPos.X + padding, tabStartY));
                this.DrawPopupTab("Symbols", PopupTab.Symbols, colors, tabHeight, scale);
                ImGui.SameLine(0f, 6f * scale);
                this.DrawPopupTab("Custom", PopupTab.Custom, colors, tabHeight, scale);

                var contentStartY = tabStartY + tabHeight + contentGap;
                var contentHeight = pHeight - padding - (contentStartY - wPos.Y);
                ImGui.SetCursorScreenPos(new Vector2(wPos.X + padding, contentStartY));

                if (this.selectedPopupTab == PopupTab.Custom)
                {
                    if (cEntries.Count == 0)
                    {
                        ImGui.TextColored(colors.MutedText, "No custom entries configured.");
                    }
                    else
                    {
                        var customCellWidth = Math.Clamp(cEntries.Max(entry => ImGui.CalcTextSize(entry).X + 18f * scale), cell, gridWidth);
                        var customColumns = Math.Clamp((int)((gridWidth + spacing) / (customCellWidth + spacing)), 1, columns);
                        var customRows = Math.Max(1, (int)Math.Ceiling(cEntries.Count / (double)customColumns));
                        this.DrawEntriesGrid(idSuffix, cEntries, customColumns, customRows, customCellWidth, cell, spacing, Math.Max(cell, contentHeight), scrollWidth, colors, insertTarget, ref this.customScrollY, allowfavs: false);
                    }
                }
                else
                {
                    var favsHeight = this.DrawfavsSection(idSuffix, columns, cell, spacing, gridWidth, colors, insertTarget);
                    if (favsHeight > 0f)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(wPos.X + padding, contentStartY + favsHeight));
                    }

                    var availableGridHeight = Math.Max(cell, contentHeight - favsHeight);
                    this.DrawEntriesGrid(idSuffix, Symbols, columns, sRows, cell, cell, spacing, availableGridHeight, scrollWidth, colors, insertTarget, ref this.symbolScrollY, allowfavs: true);
                }
            }
        }
        finally
        {
            if (beginCalled)
            {
                ImGui.End();
            }
        }
    }

    private void DrawPopupTab(string label, PopupTab tab, UiColors colors, float height, float scale)
    {
        var active = this.selectedPopupTab == tab;
        using (ImRaii.PushColor(ImGuiCol.Button, active ? colors.ButtonActive : colors.Button))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, colors.ButtonHovered))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, colors.ButtonActive))
        using (ImRaii.PushColor(ImGuiCol.Text, colors.Text))
        {
            var width = Math.Max(72f * scale, ImGui.CalcTextSize(label).X + 18f * scale);
            if (ImGui.Button($"{label}##SpecialCharacterInputTab{label}", new Vector2(width, height)))
            {
                this.selectedPopupTab = tab;
            }
        }
    }

    private IReadOnlyList<string> GetcEntries()
    {
        this.Config.Custom ??= [];
        return this.Config.Custom.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToArray();
    }

    private float DrawfavsSection(string idSuffix, int columns, float cell, float spacing, float gridWidth, UiColors colors, SymbolInsertTarget insertTarget)
    {
        var favs = this.Getfavsymbols();
        if (favs.Count == 0)
        {
            return 0f;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var dList = ImGui.GetWindowDrawList();
        var label = "favs";
        var labelHeight = 20f * scale;
        var rows = (int)Math.Ceiling(favs.Count / (double)columns);
        var favsGridHeight = rows * cell + Math.Max(0, rows - 1) * spacing;
        var dividerY = origin.Y + labelHeight + favsGridHeight + 6f * scale;

        ImGui.TextColored(colors.MutedText, label);

        IDisposable? pushedFont = null;
        if (this.symbolFont is { Available: true })
        {
            pushedFont = this.symbolFont.Push();
        }

        for (var i = 0; i < favs.Count; i++)
        {
            var row = i / columns;
            var col = i % columns;
            var cellMin = new Vector2(origin.X + col * (cell + spacing), origin.Y + labelHeight + row * (cell + spacing));
            this.DrawSymbolCell(favs[i], $"{idSuffix}-favorite-{i}", cellMin, new Vector2(cell, cell), colors, isFavorite: true, insertTarget, allowFavoriteToggle: true);
        }

        pushedFont?.Dispose();

        dList.AddLine(
            new Vector2(origin.X, dividerY),
            new Vector2(origin.X + gridWidth, dividerY),
            Color(colors.CellBorder),
            Math.Max(1f, scale));

        var totalHeight = labelHeight + favsGridHeight + 12f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + totalHeight));
        return totalHeight;
    }

    private void DrawEntriesGrid(
        string idSuffix,
        IReadOnlyList<string> entries,
        int columns,
        int rows,
        float cellWidth,
        float cellHeight,
        float spacing,
        float gridHeight,
        float scrollWidth,
        UiColors colors, SymbolInsertTarget insertTarget,
        ref float scrollY,
        bool allowfavs)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = cellHeight + spacing;
        var gridWidth = columns * cellWidth + Math.Max(0, columns - 1) * spacing;
        var gridSize = new Vector2(gridWidth + scrollWidth + 8f * scale, gridHeight);
        var maxScroll = Math.Max(0f, rows * rowHeight - spacing - gridHeight);

        scrollY = Math.Clamp(scrollY, 0f, maxScroll);

        var childFlags = ImGuiWindowFlags.NoScrollbar
                         | ImGuiWindowFlags.NoScrollWithMouse
                         | ImGuiWindowFlags.NoNav;

        if (ImGui.BeginChild($"##SpecialCharacterInputGridChild{idSuffix}{this.selectedPopupTab}", gridSize, false, childFlags))
        {
            var childOrigin = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
            {
                var wheel = ImGui.GetIO().MouseWheel;
                if (Math.Abs(wheel) > 0.01f)
                {
                    scrollY = Math.Clamp(scrollY - wheel * rowHeight * 2f, 0f, maxScroll);
                }
            }

            var firstRow = Math.Max(0, (int)Math.Floor(scrollY / rowHeight));
            var lastRow = Math.Min(rows - 1, (int)Math.Ceiling((scrollY + gridHeight) / rowHeight));

            IDisposable? pushedFont = null;
            if (this.symbolFont is { Available: true })
            {
                pushedFont = this.symbolFont.Push();
            }

            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= entries.Count)
                    {
                        break;
                    }

                    var entry = entries[index];
                    var cellMin = childOrigin + new Vector2(col * (cellWidth + spacing), row * rowHeight - scrollY);
                    var cellMax = cellMin + new Vector2(cellWidth, cellHeight);

                    if (cellMax.Y < childOrigin.Y || cellMin.Y > childOrigin.Y + gridHeight)
                    {
                        continue;
                    }

                    this.DrawSymbolCell(entry, $"{idSuffix}-entry-{this.selectedPopupTab}-{index}", cellMin, new Vector2(cellWidth, cellHeight), colors, allowfavs && this.IsFavorite(entry), insertTarget, allowfavs);
                }
            }

            pushedFont?.Dispose();

            // Custom scrollbar
            if (maxScroll > 0f)
            {
                var barX = childOrigin.X + gridWidth + 6f * scale;
                var barMin = new Vector2(barX, childOrigin.Y);
                var barMax = new Vector2(barX + scrollWidth, childOrigin.Y + gridHeight);
                var thumbHeight = Math.Max(18f * scale, gridHeight * (gridHeight / (gridHeight + maxScroll)));
                var thumbY = childOrigin.Y + (gridHeight - thumbHeight) * (scrollY / maxScroll);
                var thumbMin = new Vector2(barX, thumbY);
                var thumbMax = new Vector2(barX + scrollWidth, thumbY + thumbHeight);

                var mouse = ImGui.GetIO().MousePos;
                var tHover = mouse.X >= thumbMin.X - 4f * scale && mouse.X <= thumbMax.X + 4f * scale && mouse.Y >= thumbMin.Y && mouse.Y <= thumbMax.Y;
                var trackHover = mouse.X >= barMin.X - 5f * scale && mouse.X <= barMax.X + 5f * scale && mouse.Y >= barMin.Y && mouse.Y <= barMax.Y;

                if (tHover && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    this.draggingScrollBar = true;
                    this.scrollDragOffsetY = mouse.Y - thumbY;
                }
                else if (!tHover && trackHover && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    this.draggingScrollBar = true;
                    this.scrollDragOffsetY = thumbHeight * 0.5f;
                    var targetThumbY = Math.Clamp(mouse.Y - this.scrollDragOffsetY, childOrigin.Y, childOrigin.Y + gridHeight - thumbHeight);
                    scrollY = Math.Clamp(((targetThumbY - childOrigin.Y) / Math.Max(1f, gridHeight - thumbHeight)) * maxScroll, 0f, maxScroll);
                }

                if (this.draggingScrollBar)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        var targetThumbY = Math.Clamp(mouse.Y - this.scrollDragOffsetY, childOrigin.Y, childOrigin.Y + gridHeight - thumbHeight);
                        scrollY = Math.Clamp(((targetThumbY - childOrigin.Y) / Math.Max(1f, gridHeight - thumbHeight)) * maxScroll, 0f, maxScroll);
                    }
                    else
                    {
                        this.draggingScrollBar = false;
                    }
                }

                thumbY = childOrigin.Y + (gridHeight - thumbHeight) * (scrollY / maxScroll);
                thumbMin = new Vector2(barX, thumbY);
                thumbMax = new Vector2(barX + scrollWidth, thumbY + thumbHeight);

                drawList.AddRectFilled(barMin, barMax, Color(colors.ScrollTrack), scrollWidth * 0.5f);
                drawList.AddRectFilled(thumbMin, thumbMax, Color((tHover || this.draggingScrollBar) ? colors.ButtonHovered : colors.ScrollThumb), scrollWidth * 0.5f);
            }
            else
            {
                this.draggingScrollBar = false;
            }
        }

        ImGui.EndChild();
    }

    private void DrawSymbolCell(string symbol, string id, Vector2 cellMin, Vector2 cellSize, UiColors colors, bool isFavorite, SymbolInsertTarget insertTarget, bool allowFavoriteToggle)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var cellMax = cellMin + cellSize;

        ImGui.SetCursorScreenPos(cellMin);
        ImGui.PushID(id);
        var clicked = ImGui.InvisibleButton("##symbol", cellSize);
        var hovered = ImGui.IsItemHovered();
        ImGui.PopID();

        drawList.AddRectFilled(cellMin, cellMax, Color(hovered ? colors.CellHovered : colors.CellBackground), 5f * scale);
        drawList.AddRect(cellMin, cellMax, Color(isFavorite ? colors.Border : colors.CellBorder), 5f * scale, ImDrawFlags.None, Math.Max(1f, scale));

        var textSize = ImGui.CalcTextSize(symbol);
        var textPos = cellMin + (cellSize - textSize) * 0.5f;
        var clipMin = cellMin + new Vector2(3f * scale, 1f * scale);
        var clipMax = cellMax - new Vector2(3f * scale, 1f * scale);
        ImGui.PushClipRect(clipMin, clipMax, true);
        drawList.AddText(textPos, Color(colors.SymbolText), symbol);
        ImGui.PopClipRect();

        if (hovered)
        {
            if (allowFavoriteToggle)
            {
                ImGui.SetTooltip(isFavorite ? "CTRL+Click to Unfavorite" : "CTRL+Click to Favorite");
            }
            else
            {
                ImGui.SetTooltip(symbol);
            }
        }

        if (!clicked)
        {
            return;
        }

        if (allowFavoriteToggle && ImGui.GetIO().KeyCtrl)
        {
            this.ToggleFavorite(symbol);
        }
        else
        {
            this.QueueInsertSymbol(symbol, insertTarget);
        }
    }

    private void QueueInsertSymbol(string symbol, SymbolInsertTarget insertTarget)
    {
        if (insertTarget == SymbolInsertTarget.FocusedTextInput)
        {
            this.InsertTextIntoFocusedTextInput(symbol);
            return;
        }

        if (insertTarget == SymbolInsertTarget.RecruitmentComment)
        {
            this.InsertTextIntoRecruitmentComment(symbol);
            return;
        }

        if (insertTarget == SymbolInsertTarget.MessageBookInput)
        {
            this.InsertTextIntoMessageBook(symbol);
            return;
        }

        _ = Service.Framework.RunOnTick(() => this.InsertTextIntoChat(symbol), delayTicks: 2);
    }

    private void AdvanceCaretOnNextTick(Action<int> advanceCaret, string insertedText)
    {
        var caretMoves = GetCaretMoveCount(insertedText);
        if (caretMoves <= 0)
        {
            return;
        }

        _ = Service.Framework.RunOnTick(() => advanceCaret(caretMoves), delayTicks: 1);
    }

    private void InsertTextIntoFocusedTextInput(string text)
    {
        try
        {
            var textInput = this.keybindTextInput;
            if (textInput == null || !textInput->Enabled)
            {
                textInput = GetFocusedTextInput();
            }

            if (textInput == null || !textInput->Enabled)
            {
                return;
            }

            textInput->InsertText(text, false);
            this.AdvanceCaretOnNextTick(this.AdvanceFocusedTextInputCaretRightIfStillActive, text);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert Special Character Input text into focused text input.");
        }
    }

    private void AdvanceFocusedTextInputCaretRightIfStillActive(int caretMoves)
    {
        try
        {
            var textInput = this.keybindTextInput;
            if (textInput == null || !textInput->Enabled)
            {
                textInput = GetFocusedTextInput();
            }

            if (textInput == null || !textInput->Enabled)
            {
                return;
            }

            SendRightArrowKeyPress(caretMoves);
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance Special Character Input focused text input caret after insertion. {ex}");
        }
    }

    private void InsertTextIntoChat(string text)
    {
        try
        {
            var chatUnit = Service.GameGui.GetAddonByName(ChatLogAddonName);
            var chatLog = Service.GameGui.GetAddonByName<AddonChatLog>(ChatLogAddonName);

            if (chatUnit.IsNull || !chatUnit.IsReady || !chatUnit.IsVisible || chatLog == null || chatLog->TextInput == null)
            {
                return;
            }

            // Keep the native chat input in control; forcing focus here can desync its cursor state
            var textInput = chatLog->TextInput;
            if (!textInput->IsActive)
            {
                return;
            }

            textInput->InsertText(text, false);

            this.AdvanceCaretOnNextTick(this.AdvanceChatCaretRightIfStillActive, text);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert SpecialCharacterInput text into chat input.");
        }
    }

    private void InsertTextIntoRecruitmentComment(string text)
    {
        try
        {
            if (!this.TryGetRecruitmentCommentTarget(out var target) || target.Input == null || target.Addon == null || target.Node == null)
            {
                return;
            }

            // Only insert while the native field is active. Rebuilding the whole buffer is risky here.
            if (!target.Input->IsActive)
            {
                return;
            }

            target.Input->InsertText(text, false);

            this.AdvanceCaretOnNextTick(this.AdvanceRecruitmentCommentCaretRightIfStillActive, text);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert SpecialCharacterInput text into the Party Finder recruitment comment input.");
        }
    }

    private void AdvanceChatCaretRightIfStillActive(int caretMoves)
    {
        try
        {
            var chatUnit = Service.GameGui.GetAddonByName(ChatLogAddonName);
            var chatLog = Service.GameGui.GetAddonByName<AddonChatLog>(ChatLogAddonName);

            if (chatUnit.IsNull || !chatUnit.IsReady || !chatUnit.IsVisible || chatLog == null || chatLog->TextInput == null)
            {
                return;
            }

            if (!chatLog->TextInput->IsActive)
            {
                return;
            }

            SendRightArrowKeyPress(caretMoves);
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance SpecialCharacterInput chat input caret after insertion. {ex}");
        }
    }

    private void AdvanceRecruitmentCommentCaretRightIfStillActive(int caretMoves)
    {
        try
        {
            if (!this.TryGetRecruitmentCommentTarget(out var target) || target.Input == null)
            {
                return;
            }

            if (!target.Input->IsActive)
            {
                return;
            }

            SendRightArrowKeyPress(caretMoves);
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance SpecialCharacterInput recruitment comment caret after insertion. {ex}");
        }
    }

    private void InsertTextIntoMessageBook(string text)
    {
        try
        {
            if (!this.TryGetMessageBookInputTarget(out var target) || target.Input == null || target.Addon == null || target.Node == null)
            {
                return;
            }

            if (!target.Input->IsActive)
            {
                return;
            }

            target.Input->InsertText(text, false);

            this.AdvanceCaretOnNextTick(this.AdvanceMessageBookCaretRightIfStillActive, text);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert SpecialCharacterInput text into the Message Book input.");
        }
    }

    private void AdvanceMessageBookCaretRightIfStillActive(int caretMoves)
    {
        try
        {
            if (!this.TryGetMessageBookInputTarget(out var target) || target.Input == null)
            {
                return;
            }

            if (!target.Input->IsActive)
            {
                return;
            }

            SendRightArrowKeyPress(caretMoves);
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance SpecialCharacterInput Message Book caret after insertion. {ex}");
        }
    }

    // Addons (PF/Guestbook) searching logic
    private bool TryGetRecruitmentCommentTarget(out TextInputTarget target)
    {
        target = default;

        foreach (var addonName in RecruitmentCriteriaAddonNames)
        {
            var addonPtr = Service.GameGui.GetAddonByName(addonName);
            if (addonPtr.IsNull)
            {
                continue;
            }

            var addon = (AtkUnitBase*)addonPtr.Address;
            if (addon == null || !addon->IsReady || !addon->IsVisible || addon->RootNode == null)
            {
                continue;
            }

            var scale = Math.Clamp(addon->Scale, 0.65f, 2.4f);
            var candidates = new List<TextInputTarget>();

            // Scan paths so the button can appear in Party Finder even when the Comment field is not reachable through RootNode recursion alone
            CollectTextInputTargetsFromNodeList(addon, scale, candidates);
            CollectTextInputTargetsFromTree(addon, addon->RootNode, scale, candidates, 0);

            var best = PickBestRecruitmentCommentCandidate(candidates);
            if (best.Input == null)
            {
                continue;
            }

            target = best;
            return true;
        }

        return false;
    }

    private bool TryGetMessageBookInputTarget(out TextInputTarget target)
    {
        target = default;

        foreach (var addonName in MessageBookInputAddonNames)
        {
            var addonPtr = Service.GameGui.GetAddonByName(addonName);
            if (addonPtr.IsNull)
            {
                continue;
            }

            var addon = (AtkUnitBase*)addonPtr.Address;
            if (addon == null || !addon->IsReady || !addon->IsVisible || addon->RootNode == null)
            {
                continue;
            }

            var scale = Math.Clamp(addon->Scale, 0.65f, 2.4f);
            var candidates = new List<TextInputTarget>();

            CollectTextInputTargetsFromNodeList(addon, scale, candidates);
            CollectTextInputTargetsFromTree(addon, addon->RootNode, scale, candidates, 0);

            var best = PickBestMessageBookInputCandidate(candidates);
            if (best.Input == null)
            {
                continue;
            }

            target = best;
            return true;
        }

        return false;
    }

    private static TextInputTarget PickBestRecruitmentCommentCandidate(List<TextInputTarget> candidates)
    {
        if (candidates.Count == 0)
        {
            return default;
        }

        var minimumWidth = 140f * ImGuiHelpers.GlobalScale;
        var minimumHeight = 24f * ImGuiHelpers.GlobalScale;

        var best = candidates
            .Where(candidate => candidate.Size.X >= minimumWidth && candidate.Size.Y >= minimumHeight)
            .OrderByDescending(candidate => candidate.Size.X * candidate.Size.Y)
            .ThenBy(candidate => candidate.Position.Y)
            .FirstOrDefault();

        if (best.Input != null)
        {
            return best;
        }

        return candidates
            .OrderByDescending(candidate => candidate.Size.X * candidate.Size.Y)
            .FirstOrDefault();
    }

    private static TextInputTarget PickBestMessageBookInputCandidate(List<TextInputTarget> candidates)
    {
        if (candidates.Count == 0)
        {
            return default;
        }

        var minimumWidth = 160f * ImGuiHelpers.GlobalScale;
        var minimumHeight = 22f * ImGuiHelpers.GlobalScale;

        var best = candidates
            .Where(candidate => candidate.Size.X >= minimumWidth && candidate.Size.Y >= minimumHeight)
            .OrderByDescending(candidate => candidate.Size.X)
            .ThenByDescending(candidate => candidate.Size.Y)
            .FirstOrDefault();

        if (best.Input != null)
        {
            return best;
        }

        return candidates
            .OrderByDescending(candidate => candidate.Size.X * candidate.Size.Y)
            .FirstOrDefault();
    }

    private static void CollectTextInputTargetsFromNodeList(AtkUnitBase* addon, float scale, List<TextInputTarget> output)
    {
        if (addon == null || addon->UldManager.NodeList == null || addon->UldManager.NodeListCount <= 0)
        {
            return;
        }

        var count = Math.Min((uint)addon->UldManager.NodeListCount, 4096u);
        for (var i = 0u; i < count; i++)
        {
            AddTextInputTargetFromNode(addon, addon->UldManager.NodeList[i], scale, output);
        }
    }

    private static void CollectTextInputTargetsFromTree(AtkUnitBase* addon, AtkResNode* startNode, float scale, List<TextInputTarget> output, int depth)
    {
        if (addon == null || startNode == null || depth > 64)
        {
            return;
        }

        var node = startNode;
        var guard = 0;
        while (node != null && guard++ < 4096)
        {
            AddTextInputTargetFromNode(addon, node, scale, output);

            if (node->ChildNode != null)
            {
                CollectTextInputTargetsFromTree(addon, node->ChildNode, scale, output, depth + 1);
            }

            node = node->NextSiblingNode;
        }
    }

    private static void AddTextInputTargetFromNode(AtkUnitBase* addon, AtkResNode* node, float scale, List<TextInputTarget> output)
    {
        if (addon == null || node == null || !node->IsVisible())
        {
            return;
        }

        var input = node->GetAsAtkComponentTextInput();
        if (input == null || !input->Enabled)
        {
            return;
        }

        var size = GetNodeScreenSize(node, scale);
        if (size.X <= 10f || size.Y <= 10f)
        {
            return;
        }

        var position = new Vector2(node->ScreenX, node->ScreenY);

        // Avoid exact duplicate entries
        foreach (var existing in output)
        {
            if (existing.Node == node)
            {
                return;
            }
        }

        output.Add(new TextInputTarget(addon, input, node, position, size));
    }

    private static int GetCaretMoveCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Clamp(StringInfo.ParseCombiningCharacters(text).Length, 1, 128);
    }

    // Win32 Interop to simulate arrows
    private static void SendRightArrowKeyPress(int caretMoves)
    {
        if (!OperatingSystem.IsWindows() || caretMoves <= 0)
        {
            return;
        }

        var inputs = new Input[Math.Clamp(caretMoves, 1, 128) * 2];
        for (var i = 0; i < inputs.Length; i += 2)
        {
            inputs[i] = Input.Keyboard(VirtualKeyRight, 0);
            inputs[i + 1] = Input.Keyboard(VirtualKeyRight, KeyEventKeyUp);
        }

        _ = SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<Input>());
    }

    private const ushort VirtualKeyRight = 0x27;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;

        public static Input Keyboard(ushort virtualKey, uint flags)
        {
            return new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        ScanCode = 0,
                        Flags = flags,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero,
                    },
                },
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, ref Input inputs, int sizeOfInputStructure);

    // Configuration helpers
    private List<string> Getfavsymbols()
    {
        this.Config.favsymbols ??= new List<string>();
        if (this.Config.favsymbols.Count <= 1)
        {
            return this.Config.favsymbols;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;
        for (var i = this.Config.favsymbols.Count - 1; i >= 0; i--)
        {
            var symbol = this.Config.favsymbols[i];
            if (string.IsNullOrWhiteSpace(symbol) || !seen.Add(symbol))
            {
                this.Config.favsymbols.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            this.SaveConfig(this.Config);
        }

        return this.Config.favsymbols;
    }

    private bool IsFavorite(string symbol)
    {
        return this.Getfavsymbols().Contains(symbol, StringComparer.Ordinal);
    }

    private void ToggleFavorite(string symbol)
    {
        var favs = this.Getfavsymbols();
        var existingIndex = favs.FindIndex(item => string.Equals(item, symbol, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            favs.RemoveAt(existingIndex);
        }
        else
        {
            favs.Add(symbol);
        }

        this.SaveConfig(this.Config);
    }

    private void SaveConfigurationIfDirty()
    {
        if (!this.bPositionDirty)
        {
            return;
        }

        this.SaveConfig(this.Config);
        this.bPositionDirty = false;
    }

    private static Vector2 GetNodeScreenSize(AtkResNode* node, float scale)
    {
        var scaleX = Math.Abs(node->ScaleX);
        var scaleY = Math.Abs(node->ScaleY);
        if (scaleX <= 0.01f)
        {
            scaleX = 1f;
        }

        if (scaleY <= 0.01f)
        {
            scaleY = 1f;
        }

        return new Vector2(node->Width * scaleX * scale, node->Height * scaleY * scale);
    }

    private static Vector2 ClampPositionToScreen(Vector2 position, Vector2 size)
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        return new Vector2(
            Math.Clamp(position.X, 0f, Math.Max(0f, displaySize.X - size.X)),
            Math.Clamp(position.Y, 0f, Math.Max(0f, displaySize.Y - size.Y)));
    }

    private static uint Color(Vector4 color)
    {
        return ImGui.GetColorU32(color);
    }

    private static string[] BuildSymbols()
    {
        var symbols = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string symbol)
        {
            if (!string.IsNullOrWhiteSpace(symbol) && seen.Add(symbol))
            {
                symbols.Add(symbol);
            }
        }

        void AddRange(int startInclusive, int endInclusive)
        {
            for (var codepoint = startInclusive; codepoint <= endInclusive; codepoint++)
            {
                Add(char.ConvertFromUtf32(codepoint));
            }
        }

        void AddTextSymbols(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text, i))
                {
                    continue;
                }

                var codepoint = char.ConvertToUtf32(text, i);
                if (char.IsHighSurrogate(text[i]))
                {
                    i++;
                }

                Add(char.ConvertFromUtf32(codepoint));
            }
        }

        AddTextSymbols("★☆♠ ♡ ♢ ♣ ♤ ♥ ♦ ♧♪ ♭ ♯ °。・ ○ ◎ ● □ ■ △ ▼ ◆ ◇☀ ☁ ☂ ☃ ℃ ℉← ↑ → ↓ ⇔ ⇒ © ® ™ ℡ № § ¶ $ € ¥ £ ¢ ¤ 円∀ ∂ ∃ ⊇ ⊂ ≠ ≡ ≦ ∽ ∫ ∥ ∙ ∋ ∀ + - = ┓┗ ┐└ ┏┛ 』『」「┘┌├┝ ┥┤┣┠ ┨┫┰ ┯ ┬ ┳ ┴  ┷ ┼ ┻ ┸ ┿ ╂ ╂ ┿ ╋ 〒⊥∟⓪ ① ② ③ ④ ⑤ ⑥ ⑦ ⑧ ⑨ ⑩ ⑪ ⑫ ⑬ ⑭ ⑮ ⑯ ⑰ ⑱ ⑲ ⑳ ⑴ ⑵ ⑶ ⑷ ⑸ ⑹ ⑺ ⑻ ⑼ ⑽ ⑾ ⑿ ⒀ ⒁ ⒂ ⒃ ⒄ ⒅ ⒆ ⒇ ⒈⒉⒊⒋⒌⒍⒎⒏⒐㎎ ㎏ ㎜ ㎝ ㎞ ㎡ ㏄ ‐ –— ―‘’‚“”„†‡•‥…‰′  ⌒ ♣ ω  εïз ✓ ♀ † Å");

        AddRange(0xE020, 0xE02B);
        AddRange(0xE031, 0xE035);
        AddRange(0xE037, 0xE03F);
        AddRange(0xE040, 0xE044);
        AddRange(0xE048, 0xE04E);
        AddRange(0xE050, 0xE05F);
        AddRange(0xE060, 0xE06F);
        AddRange(0xE070, 0xE07F);
        AddRange(0xE080, 0xE08A);
        AddRange(0xE08F, 0xE08F);
        AddRange(0xE090, 0xE09F);
        AddRange(0xE0A0, 0xE0AF);
        AddRange(0xE0B0, 0xE0BF);
        AddRange(0xE0C0, 0xE0C6);
        AddRange(0xE0D0, 0xE0DB);
        AddRange(0xE0E0, 0xE0E9);

        return symbols.ToArray();
    }


    // Auxiliary Types
    private enum PopupPlacement { AboveRight, Below }
    private enum PopupTab { Symbols, Custom }
    private enum SymbolInsertTarget { Chat, RecruitmentComment, MessageBookInput, FocusedTextInput }
    private readonly unsafe struct TextInputTarget
    {
        public TextInputTarget(AtkUnitBase* addon, AtkComponentTextInput* input, AtkResNode* node, Vector2 position, Vector2 size)
        {
            this.Addon = addon;
            this.Input = input;
            this.Node = node;
            this.Position = position;
            this.Size = size;
        }

        public AtkUnitBase* Addon { get; }
        public AtkComponentTextInput* Input { get; }
        public AtkResNode* Node { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
    }

    private enum GameUiTheme
    {
        Dark = 0,
        Light = 1,
        ClassicFF = 2,
        ClearBlue = 3,
        ClearWhite = 4,
        ClearGreen = 5,
        ClearGrey = 6,
        ClearPink = 7,
        Unknown = 255,
    }

    private static GameUiTheme GetCurrentGameUiTheme()
    {
        try
        {
            if (Service.GameConfig.System.TryGet("ColorThemeType", out uint themeId))
            {
                return themeId switch
                {
                    0 => GameUiTheme.Dark,
                    1 => GameUiTheme.Light,
                    2 => GameUiTheme.ClassicFF,
                    3 => GameUiTheme.ClearBlue,
                    4 => GameUiTheme.ClearWhite,
                    5 => GameUiTheme.ClearGreen,
                    6 => GameUiTheme.ClearGrey,
                    7 => GameUiTheme.ClearPink,
                    _ => GameUiTheme.Unknown,
                };
            }
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Could not read ColorThemeType from game config. {ex}");
        }

        return GameUiTheme.Unknown;
    }

    private readonly struct UiColors
    {
        public static readonly UiColors Default = Dark;

        private static readonly UiColors Dark = Create(
            popup: new Vector4(0.035f, 0.040f, 0.050f, 0.62f),
            button: new Vector4(0.105f, 0.110f, 0.125f, 0.90f),
            border: new Vector4(0.74f, 0.68f, 0.50f, 0.62f),
            text: new Vector4(0.94f, 0.94f, 0.92f, 1f),
            muted: new Vector4(0.62f, 0.62f, 0.66f, 0.74f),
            symbol: new Vector4(1f, 1f, 1f, 1f),
            clearStyle: false);

        private static readonly UiColors Light = Create(
            popup: new Vector4(0.72f, 0.66f, 0.55f, 0.66f),
            button: new Vector4(0.60f, 0.52f, 0.41f, 0.90f),
            border: new Vector4(0.34f, 0.27f, 0.19f, 0.54f),
            text: new Vector4(0.13f, 0.11f, 0.09f, 1f),
            muted: new Vector4(0.20f, 0.18f, 0.15f, 0.68f),
            symbol: new Vector4(0.10f, 0.08f, 0.06f, 1f),
            clearStyle: false);

        private static readonly UiColors ClassicFF = Create(
            popup: new Vector4(0.010f, 0.055f, 0.310f, 0.72f),
            button: new Vector4(0.020f, 0.090f, 0.470f, 0.92f),
            border: new Vector4(0.88f, 0.88f, 1.00f, 0.72f),
            text: new Vector4(0.98f, 0.98f, 1.00f, 1f),
            muted: new Vector4(0.80f, 0.82f, 0.95f, 0.76f),
            symbol: new Vector4(1f, 1f, 1f, 1f),
            clearStyle: false);

        private static readonly UiColors ClearBlue = Create(
            popup: new Vector4(0.145f, 0.265f, 0.455f, 0.56f),
            button: new Vector4(0.170f, 0.335f, 0.620f, 0.76f),
            border: new Vector4(0.66f, 0.80f, 1.00f, 0.54f),
            text: new Vector4(0.95f, 0.98f, 1.00f, 1f),
            muted: new Vector4(0.75f, 0.86f, 1.00f, 0.78f),
            symbol: new Vector4(1f, 1f, 1f, 1f),
            clearStyle: true);

        private static readonly UiColors ClearWhite = Create(
            popup: new Vector4(0.88f, 0.90f, 0.94f, 0.58f),
            button: new Vector4(0.88f, 0.91f, 0.96f, 0.78f),
            border: new Vector4(0.30f, 0.36f, 0.45f, 0.46f),
            text: new Vector4(0.08f, 0.10f, 0.13f, 1f),
            muted: new Vector4(0.20f, 0.23f, 0.28f, 0.66f),
            symbol: new Vector4(0.04f, 0.05f, 0.07f, 1f),
            clearStyle: true);

        private static readonly UiColors ClearGreen = Create(
            popup: new Vector4(0.125f, 0.335f, 0.265f, 0.56f),
            button: new Vector4(0.120f, 0.400f, 0.315f, 0.76f),
            border: new Vector4(0.70f, 0.95f, 0.82f, 0.52f),
            text: new Vector4(0.94f, 1.00f, 0.96f, 1f),
            muted: new Vector4(0.74f, 0.95f, 0.82f, 0.76f),
            symbol: new Vector4(1f, 1f, 1f, 1f),
            clearStyle: true);

        private static readonly UiColors ClearGrey = Create(
            popup: new Vector4(0.270f, 0.285f, 0.305f, 0.56f),
            button: new Vector4(0.345f, 0.365f, 0.390f, 0.76f),
            border: new Vector4(0.78f, 0.82f, 0.86f, 0.50f),
            text: new Vector4(0.96f, 0.97f, 0.98f, 1f),
            muted: new Vector4(0.78f, 0.80f, 0.84f, 0.76f),
            symbol: new Vector4(1f, 1f, 1f, 1f),
            clearStyle: true);

        private static readonly UiColors ClearPink = Create(
            popup: new Vector4(0.480f, 0.185f, 0.315f, 0.56f),
            button: new Vector4(0.620f, 0.250f, 0.425f, 0.76f),
            border: new Vector4(1.00f, 0.72f, 0.86f, 0.52f),
            text: new Vector4(1.00f, 0.95f, 0.98f, 1f),
            muted: new Vector4(1.00f, 0.77f, 0.90f, 0.76f),
            symbol: new Vector4(1f, 1f, 1f, 1f),
            clearStyle: true);

        public UiColors(
            Vector4 PopupBackground,
            Vector4 Button,
            Vector4 ButtonHovered,
            Vector4 ButtonActive,
            Vector4 EditButton,
            Vector4 EditButtonHovered,
            Vector4 Border,
            Vector4 CellBackground,
            Vector4 CellHovered,
            Vector4 CellBorder,
            Vector4 Text,
            Vector4 SymbolText,
            Vector4 MutedText,
            Vector4 ScrollTrack,
            Vector4 ScrollThumb)
        {
            this.PopupBackground = PopupBackground;
            this.Button = Button;
            this.ButtonHovered = ButtonHovered;
            this.ButtonActive = ButtonActive;
            this.EditButton = EditButton;
            this.EditButtonHovered = EditButtonHovered;
            this.Border = Border;
            this.CellBackground = CellBackground;
            this.CellHovered = CellHovered;
            this.CellBorder = CellBorder;
            this.Text = Text;
            this.SymbolText = SymbolText;
            this.MutedText = MutedText;
            this.ScrollTrack = ScrollTrack;
            this.ScrollThumb = ScrollThumb;
        }

        public Vector4 PopupBackground { get; }
        public Vector4 Button { get; }
        public Vector4 ButtonHovered { get; }
        public Vector4 ButtonActive { get; }
        public Vector4 EditButton { get; }
        public Vector4 EditButtonHovered { get; }
        public Vector4 Border { get; }
        public Vector4 CellBackground { get; }
        public Vector4 CellHovered { get; }
        public Vector4 CellBorder { get; }
        public Vector4 Text { get; }
        public Vector4 SymbolText { get; }
        public Vector4 MutedText { get; }
        public Vector4 ScrollTrack { get; }
        public Vector4 ScrollThumb { get; }

        public static UiColors FromGameTheme(GameUiTheme theme, AddonChatLog* chatLog)
        {
            return theme switch
            {
                GameUiTheme.Dark => Dark,
                GameUiTheme.Light => Light,
                GameUiTheme.ClassicFF => ClassicFF,
                GameUiTheme.ClearBlue => ClearBlue,
                GameUiTheme.ClearWhite => ClearWhite,
                GameUiTheme.ClearGreen => ClearGreen,
                GameUiTheme.ClearGrey => ClearGrey,
                GameUiTheme.ClearPink => ClearPink,
                _ => FromChatLogFallback(chatLog),
            };
        }

        private static UiColors Create(Vector4 popup, Vector4 button, Vector4 border, Vector4 text, Vector4 muted, Vector4 symbol, bool clearStyle)
        {
            var hoverLift = IsLight(text) ? -0.055f : 0.075f;
            var activeLift = IsLight(text) ? -0.100f : -0.055f;
            var cellAlpha = clearStyle ? 0.16f : 0.22f;
            var cellHoverAlpha = clearStyle ? 0.34f : 0.42f;
            var scrollTrackAlpha = clearStyle ? 0.22f : 0.32f;

            return new UiColors(
                PopupBackground: popup,
                Button: button,
                ButtonHovered: Lift(button, hoverLift, Math.Clamp(button.W + 0.08f, 0f, 1f)),
                ButtonActive: Lift(button, activeLift, Math.Clamp(button.W + 0.12f, 0f, 1f)),
                EditButton: new Vector4(0.78f, 0.05f, 0.05f, 0.90f),
                EditButtonHovered: new Vector4(0.95f, 0.08f, 0.08f, 0.96f),
                Border: border,
                CellBackground: WithAlpha(button, cellAlpha),
                CellHovered: WithAlpha(Lift(button, hoverLift, 1f), cellHoverAlpha),
                CellBorder: WithAlpha(border, clearStyle ? 0.22f : 0.28f),
                Text: text,
                SymbolText: symbol,
                MutedText: muted,
                ScrollTrack: WithAlpha(button, scrollTrackAlpha),
                ScrollThumb: WithAlpha(border, 0.72f));
        }

        private static UiColors FromChatLogFallback(AddonChatLog* chatLog)
        {
            if (chatLog == null)
            {
                return Default;
            }

            var baseColor = TryExtractThemeColor(chatLog, Default.Button);
            if (!IsUsableThemeColor(baseColor))
            {
                return Default;
            }

            var luminance = Luminance(baseColor);
            var text = luminance > 0.58f
                ? new Vector4(0.08f, 0.08f, 0.08f, 1f)
                : new Vector4(0.96f, 0.96f, 0.96f, 1f);
            var muted = WithAlpha(text, 0.70f);
            var symbol = text;
            var border = WithAlpha(Lift(baseColor, luminance > 0.58f ? -0.24f : 0.24f, 1f), 0.56f);

            return Create(
                popup: WithAlpha(baseColor, 0.58f),
                button: WithAlpha(baseColor, 0.86f),
                border: border,
                text: text,
                muted: muted,
                symbol: symbol,
                clearStyle: false);
        }

        private static Vector4 TryExtractThemeColor(AddonChatLog* chatLog, Vector4 fallback)
        {
            if (chatLog == null)
            {
                return fallback;
            }

            if (chatLog->BackgroundNode != null)
            {
                var color = FromNodeColor(&chatLog->BackgroundNode->AtkResNode, fallback, 0.80f);
                if (IsUsableThemeColor(color))
                {
                    return color;
                }
            }

            if (chatLog->ChannelSelectDropDown != null)
            {
                var node = chatLog->ChannelSelectDropDown->AtkComponentBase.OwnerNode;
                if (node != null)
                {
                    var color = FromNodeColor(&node->AtkResNode, fallback, 0.82f);
                    if (IsUsableThemeColor(color))
                    {
                        return color;
                    }
                }
            }

            return fallback;
        }

        private static Vector4 FromNodeColor(AtkResNode* node, Vector4 fallback, float alpha)
        {
            if (node == null)
            {
                return fallback;
            }

            var color = node->Color;
            if (color.R == 0 && color.G == 0 && color.B == 0 && color.A == 0)
            {
                return fallback;
            }

            return new Vector4(
                color.R / 255f,
                color.G / 255f,
                color.B / 255f,
                Math.Clamp((color.A / 255f) * alpha, 0.20f, 0.95f));
        }

        private static bool IsUsableThemeColor(Vector4 color)
        {
            var max = Math.Max(color.X, Math.Max(color.Y, color.Z));
            var min = Math.Min(color.X, Math.Min(color.Y, color.Z));
            var saturation = max - min;
            var luminance = Luminance(color);

            if (luminance > 0.78f && saturation < 0.12f)
            {
                return false;
            }

            return color.W > 0.05f;
        }

        private static bool IsLight(Vector4 color)
        {
            return Luminance(color) > 0.70f;
        }

        private static float Luminance(Vector4 color)
        {
            return color.X * 0.2126f + color.Y * 0.7152f + color.Z * 0.0722f;
        }

        private static Vector4 WithAlpha(Vector4 color, float alpha)
        {
            return new Vector4(color.X, color.Y, color.Z, alpha);
        }

        private static Vector4 Lift(Vector4 color, float amount, float alpha)
        {
            return new Vector4(
                Math.Clamp(color.X + amount, 0f, 1f),
                Math.Clamp(color.Y + amount, 0f, 1f),
                Math.Clamp(color.Z + amount, 0f, 1f),
                alpha);
        }
    }

}
