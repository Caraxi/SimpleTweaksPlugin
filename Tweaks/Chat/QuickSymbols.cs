using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public sealed class QuickSymbolsConfig : TweakConfig
{
    // Kept for compatibility with older Quick Symbols config files. Newer versions store the custom
    // placement as an offset from the native chat button so it follows the ChatLog.
    public bool HasCustomButtonPosition { get; set; }
    public Vector2 ButtonPosition { get; set; }
    public bool UsesRelativeButtonOffset { get; set; }
    public Vector2 ButtonOffset { get; set; }

    public List<string> FavoriteSymbols { get; set; } = new();
}

[TweakName("Quick Chat Symbols")]
[TweakDescription("Adds a Quick Symbols button beside the chat mode selector and other text inputs.")]
[TweakAuthor("Bryer")]
public sealed unsafe class QuickSymbols : ChatTweaks.SubTweak
{
    private const string ChatLogAddonName = "ChatLog";
    private const string RecruitmentCriteriaAddonName = "LookingForGroupCondition";
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
    private const string CommandShort = "/qs";
    private const string CommandLong = "/quicksymbols";
    private const int MaxColumns = 10;

    private static readonly string[] Symbols = BuildSymbols();

    private readonly List<string> registeredCommands = [];

    [TweakConfig]
    public QuickSymbolsConfig Config { get; private set; } = new();

    private IFontHandle? symbolFont;
    private bool popupOpen;
    private bool partyFinderPopupOpen;
    private bool messageBookPopupOpen;
    private bool editButtonPosition;
    private bool draggingButton;
    private bool buttonPositionDirty;
    private Vector2 nativeButtonPos;
    private Vector2 currentButtonPos;
    private Vector2 currentButtonSize;
    private Vector2 partyFinderButtonPos;
    private Vector2 partyFinderButtonSize;
    private Vector2 messageBookButtonPos;
    private Vector2 messageBookButtonSize;
    private float symbolScrollY;
    private bool draggingScrollBar;
    private float scrollDragOffsetY;

    protected override void Setup()
    {
        this.Config = this.LoadConfig<QuickSymbolsConfig>() ?? new QuickSymbolsConfig();
    }

    protected override void Enable()
    {
        this.symbolFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 18f));
        PluginInterface.UiBuilder.Draw += this.Draw;

        this.RegisterCommand(CommandShort, true);
        this.RegisterCommand(CommandLong, false);
    }

    protected override void Disable()
    {
        PluginInterface.UiBuilder.Draw -= this.Draw;

        foreach (var command in this.registeredCommands)
        {
            Service.Commands.RemoveHandler(command);
        }

        this.registeredCommands.Clear();
        this.symbolFont?.Dispose();
        this.symbolFont = null;

        this.popupOpen = false;
        this.partyFinderPopupOpen = false;
        this.messageBookPopupOpen = false;
        this.editButtonPosition = false;
        this.draggingButton = false;
        this.draggingScrollBar = false;

        this.SaveConfig(this.Config);
    }

    public override void Dispose()
    {
        this.symbolFont?.Dispose();
        this.symbolFont = null;
        base.Dispose();
    }

    private void RegisterCommand(string command, bool showInHelp)
    {
        if (Service.Commands.Commands.ContainsKey(command))
        {
            SimpleLog.Warning($"Quick Chat Symbols skipped registering command '{command}' because it is already registered.");
            return;
        }

        Service.Commands.AddHandler(command, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Quick Symbols.",
            ShowInHelp = showInHelp,
        });
        this.registeredCommands.Add(command);
    }

    private void OnCommand(string command, string args)
    {
        this.popupOpen = true;
    }

    private void Draw()
    {
        if (Service.GameGui.GameUiHidden)
        {
            return;
        }

        var gameTheme = GetCurrentGameUiTheme();
        var colors = UiColors.FromGameTheme(gameTheme, null);
        var drawChatPopup = false;
        var drawPartyFinderPopup = false;
        var drawMessageBookPopup = false;

        if (this.TryGetNativeChatButtonPlacement(out var nativePos, out var nativeSize, out colors))
        {
            this.nativeButtonPos = nativePos;
            this.currentButtonSize = nativeSize;
            this.currentButtonPos = this.GetCurrentButtonPosition(nativePos, nativeSize);

            this.DrawChatButton(this.currentButtonPos, nativeSize, colors);
            drawChatPopup = this.popupOpen;
        }
        else
        {
            this.popupOpen = false;
            this.editButtonPosition = false;
        }

        if (this.TryGetRecruitmentCommentTarget(out var recruitmentTarget))
        {
            var scale = ImGuiHelpers.GlobalScale;

            // Keep the Party Finder heart button visually small, like the chat button.
            // It still follows the Comment field position/scale, but it should not grow
            // into a large square when the native Comment box is resized taller.
            var referenceSide = this.currentButtonSize.Y > 0.1f ? this.currentButtonSize.Y : 24f * scale;
            var buttonSide = Math.Clamp(Math.Min(referenceSide, recruitmentTarget.Size.Y * 0.50f), 18f * scale, 28f * scale);
            this.partyFinderButtonSize = new Vector2(buttonSide, buttonSide);
            this.partyFinderButtonPos = ClampPositionToScreen(
                new Vector2(
                    recruitmentTarget.Position.X + 6f * scale,
                    recruitmentTarget.Position.Y + recruitmentTarget.Size.Y + 2f * scale),
                this.partyFinderButtonSize);

            this.DrawContextButton(
                "##QuickSymbolsRecruitmentCommentButtonOverlay",
                "##QuickSymbolsRecruitmentCommentOpenButton",
                this.partyFinderButtonPos,
                this.partyFinderButtonSize,
                colors,
                ref this.partyFinderPopupOpen);
            drawPartyFinderPopup = this.partyFinderPopupOpen;
        }
        else
        {
            this.partyFinderPopupOpen = false;
        }

        if (this.TryGetMessageBookInputTarget(out var messageTarget))
        {
            var scale = ImGuiHelpers.GlobalScale;
            var referenceSide = this.currentButtonSize.Y > 0.1f ? this.currentButtonSize.Y : 24f * scale;
            var buttonSide = Math.Clamp(Math.Min(referenceSide, messageTarget.Size.Y * 0.58f), 18f * scale, 28f * scale);
            this.messageBookButtonSize = new Vector2(buttonSide, buttonSide);
            this.messageBookButtonPos = ClampPositionToScreen(
                new Vector2(
                    messageTarget.Position.X + 6f * scale,
                    messageTarget.Position.Y + messageTarget.Size.Y + 3f * scale),
                this.messageBookButtonSize);

            this.DrawContextButton(
                "##QuickSymbolsMessageBookButtonOverlay",
                "##QuickSymbolsMessageBookOpenButton",
                this.messageBookButtonPos,
                this.messageBookButtonSize,
                colors,
                ref this.messageBookPopupOpen);
            drawMessageBookPopup = this.messageBookPopupOpen;
        }
        else
        {
            this.messageBookPopupOpen = false;
        }

        // Draw popups after every overlay button so they stay visually above the
        // plugin's own buttons and capture clicks before the game UI underneath.
        if (drawChatPopup)
        {
            this.DrawSymbolsPopup(
                "Chat",
                colors,
                this.currentButtonPos,
                this.currentButtonSize,
                PopupPlacement.AboveRight,
                includePositionEditor: true,
                SymbolInsertTarget.Chat,
                ref this.popupOpen);
        }

        if (drawPartyFinderPopup)
        {
            this.DrawSymbolsPopup(
                "PartyFinder",
                colors,
                this.partyFinderButtonPos,
                this.partyFinderButtonSize,
                PopupPlacement.Below,
                includePositionEditor: false,
                SymbolInsertTarget.RecruitmentComment,
                ref this.partyFinderPopupOpen);
        }

        if (drawMessageBookPopup)
        {
            this.DrawSymbolsPopup(
                "MessageBook",
                colors,
                this.messageBookButtonPos,
                this.messageBookButtonSize,
                PopupPlacement.Below,
                includePositionEditor: false,
                SymbolInsertTarget.MessageBookInput,
                ref this.messageBookPopupOpen);
        }
    }

    private Vector2 GetCurrentButtonPosition(Vector2 nativePos, Vector2 nativeSize)
    {
        if (!this.Config.HasCustomButtonPosition)
        {
            return nativePos;
        }

        if (!this.Config.UsesRelativeButtonOffset)
        {
            this.Config.ButtonOffset = this.Config.ButtonPosition - nativePos;
            this.Config.UsesRelativeButtonOffset = true;
            this.buttonPositionDirty = true;
        }

        var desired = nativePos + this.Config.ButtonOffset;
        var clamped = ClampPositionToScreen(desired, nativeSize);
        if (Vector2.DistanceSquared(desired, clamped) > 0.01f)
        {
            this.Config.ButtonOffset = clamped - nativePos;
            this.Config.ButtonPosition = clamped;
            this.buttonPositionDirty = true;
        }

        this.SaveConfigurationIfDirty();
        return clamped;
    }

    private bool TryGetNativeChatButtonPlacement(out Vector2 buttonPos, out Vector2 buttonSize, out UiColors colors)
    {
        buttonPos = Vector2.Zero;
        buttonSize = Vector2.Zero;
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

        var unitScale = Math.Clamp(chatUnit.Scale, 0.65f, 2.4f);
        var gap = Math.Max(2f, 2f * unitScale);

        if (chatLog->ChannelSelectDropDown != null)
        {
            var channelNode = chatLog->ChannelSelectDropDown->AtkComponentBase.OwnerNode;
            if (channelNode != null && channelNode->AtkResNode.IsVisible())
            {
                var resNode = &channelNode->AtkResNode;
                var nodeHeight = GetNodeScreenSize(resNode, unitScale).Y;
                var square = Math.Clamp(nodeHeight, 18f * unitScale, 28f * unitScale);

                buttonSize = new Vector2(square, square);
                buttonPos = new Vector2(
                    resNode->ScreenX - square - gap,
                    resNode->ScreenY + Math.Max(0f, (nodeHeight - square) * 0.5f));
                return true;
            }
        }

        if (chatLog->CurrentChannelTextNode != null && chatLog->CurrentChannelTextNode->AtkResNode.IsVisible())
        {
            var resNode = &chatLog->CurrentChannelTextNode->AtkResNode;
            var nodeHeight = GetNodeScreenSize(resNode, unitScale).Y;
            var square = Math.Clamp(nodeHeight + 8f * unitScale, 18f * unitScale, 28f * unitScale);

            buttonSize = new Vector2(square, square);
            buttonPos = new Vector2(
                resNode->ScreenX - square - gap,
                resNode->ScreenY - 4f * unitScale);
            return true;
        }

        var fallbackSize = Math.Clamp(24f * unitScale, 18f, 32f * unitScale);
        buttonSize = new Vector2(fallbackSize, fallbackSize);
        buttonPos = new Vector2(
            chatUnit.Position.X + 4f * unitScale,
            chatUnit.Position.Y + chatUnit.ScaledSize.Y - fallbackSize - 4f * unitScale);
        return true;
    }

    private void DrawChatButton(Vector2 position, Vector2 size, UiColors colors)
    {
        var clicked = this.DrawHeartButtonOverlay(
            "##QuickSymbolsChatButtonOverlay",
            "##QuickSymbolsOpenButton",
            position,
            size,
            colors,
            this.editButtonPosition,
            out var active);

        if (this.editButtonPosition)
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
        if (this.DrawHeartButtonOverlay(
                windowId,
                buttonId,
                position,
                size,
                colors,
                editing: false,
                out _))
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

        using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        using var windowBorder = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 0f);
        using var windowBackground = ImRaii.PushColor(ImGuiCol.WindowBg, Vector4.Zero);

        try
        {
            var windowVisible = ImGui.Begin(windowId, flags);
            beginCalled = true;
            if (windowVisible)
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
            var clamped = ClampPositionToScreen(this.currentButtonPos + io.MouseDelta, size);
            this.Config.HasCustomButtonPosition = true;
            this.Config.UsesRelativeButtonOffset = true;
            this.Config.ButtonPosition = clamped;
            this.Config.ButtonOffset = clamped - this.nativeButtonPos;
            this.currentButtonPos = clamped;
            this.buttonPositionDirty = true;
        }
        else if (this.draggingButton && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            this.draggingButton = false;
            this.SaveConfigurationIfDirty();
        }
    }

    private void DrawSymbolsPopup(
        string idSuffix,
        UiColors colors,
        Vector2 anchorPos,
        Vector2 anchorSize,
        PopupPlacement placement,
        bool includePositionEditor,
        SymbolInsertTarget insertTarget,
        ref bool isOpen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var displaySize = ImGui.GetIO().DisplaySize;
        var cell = Math.Clamp(anchorSize.Y * 1.05f, 22f * scale, 34f * scale);
        var spacing = Math.Max(3f, 4f * scale);
        var padding = Math.Max(8f, 10f * scale);
        var scrollBarWidth = Math.Max(3f, 4f * scale);
        var availableWidth = displaySize.X - 16f * scale;
        var columns = Math.Clamp((int)((availableWidth - padding * 2f - scrollBarWidth - 8f * scale + spacing) / (cell + spacing)), 1, MaxColumns);
        var totalRows = (int)Math.Ceiling(Symbols.Length / (double)columns);
        var visibleRows = Math.Min(totalRows, 8);
        var headerHeight = 26f * scale;
        var keptChatEditorSpace = includePositionEditor ? 28f * scale : 0f;
        var gridWidth = columns * cell + Math.Max(0, columns - 1) * spacing;
        var originalGridHeight = visibleRows * cell + Math.Max(0, visibleRows - 1) * spacing;
        var popupWidth = Math.Min(availableWidth, padding * 2f + gridWidth + scrollBarWidth + 8f * scale);
        var popupHeight = padding * 2f + headerHeight + keptChatEditorSpace + 8f * scale + originalGridHeight;

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
            posY = anchorPos.Y - popupHeight - 8f * scale;
        }

        posX = Math.Clamp(posX, 8f * scale, Math.Max(8f * scale, displaySize.X - popupWidth - 8f * scale));
        posY = Math.Clamp(posY, 8f * scale, Math.Max(8f * scale, displaySize.Y - popupHeight - 8f * scale));

        ImGui.SetNextWindowPos(new Vector2(posX, posY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(popupWidth, popupHeight), ImGuiCond.Always);

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

        using var popupPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(padding, padding));
        using var popupBorderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f * scale);
        using var popupRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 8f * scale);
        using var popupBackground = ImRaii.PushColor(ImGuiCol.WindowBg, colors.PopupBackground);
        using var popupBorder = ImRaii.PushColor(ImGuiCol.Border, colors.Border);

        try
        {
            var windowVisible = ImGui.Begin($"##QuickSymbolsPopup{idSuffix}", flags);
            beginCalled = true;
            if (windowVisible)
            {
                var windowPos = ImGui.GetWindowPos();
                var drawList = ImGui.GetWindowDrawList();
                var title = "Bryer - Quick Symbols";
                ImGui.TextColored(colors.MutedText, title);

                var closeSize = new Vector2(22f * scale, 22f * scale);
                var closePos = new Vector2(windowPos.X + popupWidth - padding - closeSize.X, windowPos.Y + padding - 1f * scale);

                if (includePositionEditor)
                {
                    var editLabel = this.editButtonPosition ? "Editing button position" : "Change button position";
                    var editButtonSize = new Vector2(
                        Math.Min(
                            Math.Max(126f * scale, ImGui.CalcTextSize(editLabel).X + 16f * scale),
                            Math.Max(80f * scale, closePos.X - (windowPos.X + padding + ImGui.CalcTextSize(title).X + 12f * scale) - 6f * scale)),
                        22f * scale);
                    var editButtonPos = new Vector2(windowPos.X + padding + ImGui.CalcTextSize(title).X + 12f * scale, windowPos.Y + padding - 1f * scale);

                    ImGui.SetCursorScreenPos(editButtonPos);
                    using (ImRaii.PushColor(ImGuiCol.Button, this.editButtonPosition ? colors.EditButton : colors.Button))
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, this.editButtonPosition ? colors.EditButtonHovered : colors.ButtonHovered))
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, colors.ButtonActive))
                    using (ImRaii.PushColor(ImGuiCol.Text, colors.Text))
                    {
                        if (ImGui.Button(editLabel, editButtonSize))
                        {
                            this.editButtonPosition = !this.editButtonPosition;
                            this.draggingButton = false;
                            this.SaveConfigurationIfDirty();
                        }
                    }
                }

                ImGui.SetCursorScreenPos(closePos);
                if (ImGui.InvisibleButton($"##QuickSymbolsCloseButton{idSuffix}", closeSize))
                {
                    isOpen = false;
                }

                var closeHovered = ImGui.IsItemHovered();
                drawList.AddRectFilled(closePos, closePos + closeSize, Color(closeHovered ? colors.CellHovered : colors.CellBackground), 4f * scale);
                var xText = "X";
                var xSize = ImGui.CalcTextSize(xText);
                drawList.AddText(closePos + (closeSize - xSize) * 0.5f, Color(colors.Text), xText);

                var contentStartY = windowPos.Y + padding + headerHeight + 6f * scale;
                var contentHeight = popupHeight - padding - (contentStartY - windowPos.Y);
                ImGui.SetCursorScreenPos(new Vector2(windowPos.X + padding, contentStartY));

                var favoritesHeight = this.DrawFavoritesSection(idSuffix, columns, cell, spacing, gridWidth, colors, insertTarget);
                if (favoritesHeight > 0f)
                {
                    ImGui.SetCursorScreenPos(new Vector2(windowPos.X + padding, contentStartY + favoritesHeight));
                }

                var gridHeight = Math.Max(cell, contentHeight - favoritesHeight);
                this.DrawSymbolsGrid(idSuffix, columns, totalRows, cell, spacing, gridHeight, scrollBarWidth, colors, insertTarget);
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

    private float DrawFavoritesSection(string idSuffix, int columns, float cell, float spacing, float gridWidth, UiColors colors, SymbolInsertTarget insertTarget)
    {
        var favorites = this.GetFavoriteSymbols();
        if (favorites.Count == 0)
        {
            return 0f;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var label = "Favorites";
        var labelHeight = 20f * scale;
        var rows = (int)Math.Ceiling(favorites.Count / (double)columns);
        var favoritesGridHeight = rows * cell + Math.Max(0, rows - 1) * spacing;
        var dividerY = origin.Y + labelHeight + favoritesGridHeight + 6f * scale;

        ImGui.TextColored(colors.MutedText, label);

        IDisposable? pushedFont = null;
        if (this.symbolFont is { Available: true })
        {
            pushedFont = this.symbolFont.Push();
        }

        for (var i = 0; i < favorites.Count; i++)
        {
            var row = i / columns;
            var col = i % columns;
            var cellMin = new Vector2(origin.X + col * (cell + spacing), origin.Y + labelHeight + row * (cell + spacing));
            this.DrawSymbolCell(favorites[i], $"{idSuffix}-favorite-{i}", cellMin, cell, colors, isFavorite: true, insertTarget);
        }

        pushedFont?.Dispose();

        drawList.AddLine(
            new Vector2(origin.X, dividerY),
            new Vector2(origin.X + gridWidth, dividerY),
            Color(colors.CellBorder),
            Math.Max(1f, scale));

        var totalHeight = labelHeight + favoritesGridHeight + 12f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + totalHeight));
        return totalHeight;
    }

    private void DrawSymbolsGrid(string idSuffix, int columns, int rows, float cell, float spacing, float gridHeight, float scrollBarWidth, UiColors colors, SymbolInsertTarget insertTarget)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = cell + spacing;
        var gridWidth = columns * cell + Math.Max(0, columns - 1) * spacing;
        var gridSize = new Vector2(gridWidth + scrollBarWidth + 8f * scale, gridHeight);
        var maxScroll = Math.Max(0f, rows * rowHeight - spacing - gridHeight);

        this.symbolScrollY = Math.Clamp(this.symbolScrollY, 0f, maxScroll);

        var childFlags = ImGuiWindowFlags.NoScrollbar
                         | ImGuiWindowFlags.NoScrollWithMouse
                         | ImGuiWindowFlags.NoNav;

        if (ImGui.BeginChild($"##QuickSymbolsGridChild{idSuffix}", gridSize, false, childFlags))
        {
            var childOrigin = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
            {
                var wheel = ImGui.GetIO().MouseWheel;
                if (Math.Abs(wheel) > 0.01f)
                {
                    this.symbolScrollY = Math.Clamp(this.symbolScrollY - wheel * rowHeight * 2f, 0f, maxScroll);
                }
            }

            var firstRow = Math.Max(0, (int)Math.Floor(this.symbolScrollY / rowHeight));
            var lastRow = Math.Min(rows - 1, (int)Math.Ceiling((this.symbolScrollY + gridHeight) / rowHeight));

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
                    if (index >= Symbols.Length)
                    {
                        break;
                    }

                    var symbol = Symbols[index];
                    var cellMin = childOrigin + new Vector2(col * (cell + spacing), row * rowHeight - this.symbolScrollY);
                    var cellMax = cellMin + new Vector2(cell, cell);

                    if (cellMax.Y < childOrigin.Y || cellMin.Y > childOrigin.Y + gridHeight)
                    {
                        continue;
                    }

                    this.DrawSymbolCell(symbol, $"{idSuffix}-symbol-{index}", cellMin, cell, colors, this.IsFavorite(symbol), insertTarget);
                }
            }

            pushedFont?.Dispose();

            if (maxScroll > 0f)
            {
                var barX = childOrigin.X + gridWidth + 6f * scale;
                var barMin = new Vector2(barX, childOrigin.Y);
                var barMax = new Vector2(barX + scrollBarWidth, childOrigin.Y + gridHeight);
                var thumbHeight = Math.Max(18f * scale, gridHeight * (gridHeight / (gridHeight + maxScroll)));
                var thumbY = childOrigin.Y + (gridHeight - thumbHeight) * (this.symbolScrollY / maxScroll);
                var thumbMin = new Vector2(barX, thumbY);
                var thumbMax = new Vector2(barX + scrollBarWidth, thumbY + thumbHeight);

                var mouse = ImGui.GetIO().MousePos;
                var thumbHovered = mouse.X >= thumbMin.X - 4f * scale && mouse.X <= thumbMax.X + 4f * scale && mouse.Y >= thumbMin.Y && mouse.Y <= thumbMax.Y;
                var trackHovered = mouse.X >= barMin.X - 5f * scale && mouse.X <= barMax.X + 5f * scale && mouse.Y >= barMin.Y && mouse.Y <= barMax.Y;

                if (thumbHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    this.draggingScrollBar = true;
                    this.scrollDragOffsetY = mouse.Y - thumbY;
                }
                else if (!thumbHovered && trackHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    this.draggingScrollBar = true;
                    this.scrollDragOffsetY = thumbHeight * 0.5f;
                    var targetThumbY = Math.Clamp(mouse.Y - this.scrollDragOffsetY, childOrigin.Y, childOrigin.Y + gridHeight - thumbHeight);
                    this.symbolScrollY = Math.Clamp(((targetThumbY - childOrigin.Y) / Math.Max(1f, gridHeight - thumbHeight)) * maxScroll, 0f, maxScroll);
                }

                if (this.draggingScrollBar)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        var targetThumbY = Math.Clamp(mouse.Y - this.scrollDragOffsetY, childOrigin.Y, childOrigin.Y + gridHeight - thumbHeight);
                        this.symbolScrollY = Math.Clamp(((targetThumbY - childOrigin.Y) / Math.Max(1f, gridHeight - thumbHeight)) * maxScroll, 0f, maxScroll);
                    }
                    else
                    {
                        this.draggingScrollBar = false;
                    }
                }

                thumbY = childOrigin.Y + (gridHeight - thumbHeight) * (this.symbolScrollY / maxScroll);
                thumbMin = new Vector2(barX, thumbY);
                thumbMax = new Vector2(barX + scrollBarWidth, thumbY + thumbHeight);

                drawList.AddRectFilled(barMin, barMax, Color(colors.ScrollTrack), scrollBarWidth * 0.5f);
                drawList.AddRectFilled(thumbMin, thumbMax, Color((thumbHovered || this.draggingScrollBar) ? colors.ButtonHovered : colors.ScrollThumb), scrollBarWidth * 0.5f);
            }
            else
            {
                this.draggingScrollBar = false;
            }
        }

        ImGui.EndChild();
    }

    private void DrawSymbolCell(string symbol, string id, Vector2 cellMin, float cell, UiColors colors, bool isFavorite, SymbolInsertTarget insertTarget)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var cellMax = cellMin + new Vector2(cell, cell);

        ImGui.SetCursorScreenPos(cellMin);
        ImGui.PushID(id);
        var clicked = ImGui.InvisibleButton("##symbol", new Vector2(cell, cell));
        var hovered = ImGui.IsItemHovered();
        ImGui.PopID();

        drawList.AddRectFilled(cellMin, cellMax, Color(hovered ? colors.CellHovered : colors.CellBackground), 5f * scale);
        drawList.AddRect(cellMin, cellMax, Color(isFavorite ? colors.Border : colors.CellBorder), 5f * scale, ImDrawFlags.None, Math.Max(1f, scale));

        var textSize = ImGui.CalcTextSize(symbol);
        drawList.AddText(cellMin + (new Vector2(cell, cell) - textSize) * 0.5f, Color(colors.SymbolText), symbol);

        if (hovered)
        {
            ImGui.SetTooltip(isFavorite ? "CTRL+Click to Unfavorite" : "CTRL+Click to Favorite");
        }

        if (!clicked)
        {
            return;
        }

        if (ImGui.GetIO().KeyCtrl)
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
        if (insertTarget == SymbolInsertTarget.RecruitmentComment)
        {
            // Insert immediately while the native Party Finder text input is still active.
            // Delaying this until after the ImGui click finished could make the game field
            // lose focus and leave its visual buffer temporarily duplicated.
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

            // Do not force ChatLog.Focus() here; it can desync the native chat input scroll/cursor state.
            var textInput = chatLog->TextInput;
            if (!textInput->IsActive)
            {
                return;
            }

            textInput->InsertText(text, false);

            // Keep the accepted v19 behavior: let the game advance the native caret.
            _ = Service.Framework.RunOnTick(this.AdvanceChatCaretRightIfStillActive, delayTicks: 1);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert QuickSymbols text into chat input.");
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

            // Match the safe ChatLog behavior: do not force focus or rewrite the whole
            // text buffer. Only insert into the input if it is already active.
            if (!target.Input->IsActive)
            {
                return;
            }

            target.Input->InsertText(text, false);

            _ = Service.Framework.RunOnTick(this.AdvanceRecruitmentCommentCaretRightIfStillActive, delayTicks: 1);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert QuickSymbols text into the Party Finder recruitment comment input.");
        }
    }

    private void AdvanceChatCaretRightIfStillActive()
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

            SendRightArrowKeyPress();
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance QuickSymbols chat input caret after insertion. {ex}");
        }
    }

    private void AdvanceRecruitmentCommentCaretRightIfStillActive()
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

            SendRightArrowKeyPress();
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance QuickSymbols recruitment comment caret after insertion. {ex}");
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

            _ = Service.Framework.RunOnTick(this.AdvanceMessageBookCaretRightIfStillActive, delayTicks: 1);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Failed to insert QuickSymbols text into the Message Book input.");
        }
    }

    private void AdvanceMessageBookCaretRightIfStillActive()
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

            SendRightArrowKeyPress();
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to advance QuickSymbols Message Book caret after insertion. {ex}");
        }
    }

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

            // Some game windows keep component nodes only in the ULD node list and not
            // in a clean RootNode/ChildNode traversal. Scan both paths so the button can
            // appear in Party Finder > Recruitment Criteria even when the Comment field
            // is not reachable through RootNode recursion alone.
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

        // The Recruitment Criteria comment box is the large multi-line text input in the
        // left/middle area of the window. Password/item-level inputs are smaller and sit
        // farther right, so prefer wide and taller inputs first.
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

        // The Message Book popup has one main wide text input. Prefer the widest
        // visible text field, but keep a small height threshold so buttons/labels are ignored.
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

    private static void CollectTextInputTargetsFromNodeList(AtkUnitBase* addon, float unitScale, List<TextInputTarget> output)
    {
        if (addon == null || addon->UldManager.NodeList == null || addon->UldManager.NodeListCount <= 0)
        {
            return;
        }

        var count = Math.Min((uint)addon->UldManager.NodeListCount, 4096u);
        for (var i = 0u; i < count; i++)
        {
            AddTextInputTargetFromNode(addon, addon->UldManager.NodeList[i], unitScale, output);
        }
    }

    private static void CollectTextInputTargetsFromTree(AtkUnitBase* addon, AtkResNode* startNode, float unitScale, List<TextInputTarget> output, int depth)
    {
        if (addon == null || startNode == null || depth > 64)
        {
            return;
        }

        var node = startNode;
        var guard = 0;
        while (node != null && guard++ < 4096)
        {
            AddTextInputTargetFromNode(addon, node, unitScale, output);

            if (node->ChildNode != null)
            {
                CollectTextInputTargetsFromTree(addon, node->ChildNode, unitScale, output, depth + 1);
            }

            node = node->NextSiblingNode;
        }
    }

    private static void AddTextInputTargetFromNode(AtkUnitBase* addon, AtkResNode* node, float unitScale, List<TextInputTarget> output)
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

        var size = GetNodeScreenSize(node, unitScale);
        if (size.X <= 10f || size.Y <= 10f)
        {
            return;
        }

        var position = new Vector2(node->ScreenX, node->ScreenY);

        // Avoid exact duplicate entries because the same node can be found through both
        // the ULD node list and the RootNode tree.
        foreach (var existing in output)
        {
            if (existing.Node == node)
            {
                return;
            }
        }

        output.Add(new TextInputTarget(addon, input, node, position, size));
    }

    private static void SendRightArrowKeyPress()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Span<Input> inputs = stackalloc Input[2];
        inputs[0] = Input.Keyboard(VirtualKeyRight, 0);
        inputs[1] = Input.Keyboard(VirtualKeyRight, KeyEventKeyUp);
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

    private List<string> GetFavoriteSymbols()
    {
        this.Config.FavoriteSymbols ??= new List<string>();
        if (this.Config.FavoriteSymbols.Count <= 1)
        {
            return this.Config.FavoriteSymbols;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;
        for (var i = this.Config.FavoriteSymbols.Count - 1; i >= 0; i--)
        {
            var symbol = this.Config.FavoriteSymbols[i];
            if (string.IsNullOrWhiteSpace(symbol) || !seen.Add(symbol))
            {
                this.Config.FavoriteSymbols.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            this.SaveConfig(this.Config);
        }

        return this.Config.FavoriteSymbols;
    }

    private bool IsFavorite(string symbol)
    {
        return this.GetFavoriteSymbols().Contains(symbol, StringComparer.Ordinal);
    }

    private void ToggleFavorite(string symbol)
    {
        var favorites = this.GetFavoriteSymbols();
        var existingIndex = favorites.FindIndex(item => string.Equals(item, symbol, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            favorites.RemoveAt(existingIndex);
        }
        else
        {
            favorites.Add(symbol);
        }

        this.SaveConfig(this.Config);
    }

    private void SaveConfigurationIfDirty()
    {
        if (!this.buttonPositionDirty)
        {
            return;
        }

        this.SaveConfig(this.Config);
        this.buttonPositionDirty = false;
    }

    private static Vector2 GetNodeScreenSize(AtkResNode* node, float unitScale)
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

        return new Vector2(node->Width * scaleX * unitScale, node->Height * scaleY * unitScale);
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

    private enum PopupPlacement
    {
        AboveRight,
        Below,
    }

    private enum SymbolInsertTarget
    {
        Chat,
        RecruitmentComment,
        MessageBookInput,
    }

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

    private static Vector4 WithAlpha(Vector4 color, float alpha)
    {
        return new Vector4(color.X, color.Y, color.Z, alpha);
    }

    private static Vector4 StrongButtonHover(UiColors colors)
    {
        var baseColor = colors.ButtonHovered;
        var border = colors.Border;
        return new Vector4(
            Math.Clamp(baseColor.X * 0.68f + border.X * 0.32f, 0f, 1f),
            Math.Clamp(baseColor.Y * 0.68f + border.Y * 0.32f, 0f, 1f),
            Math.Clamp(baseColor.Z * 0.68f + border.Z * 0.32f, 0f, 1f),
            Math.Clamp(Math.Max(baseColor.W, colors.Button.W) + 0.12f, 0f, 1f));
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
