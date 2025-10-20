using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Combat Timer")]
[TweakDescription("Shows a combat timer.")]
[TweakAuthor("Hakuna")]
[TweakAutoConfig]
public unsafe class CombatTimer : UiAdjustments.SubTweak
{
    private DateTime battleStartTime;
    private DateTime lastCombatEndTime;
    private bool inCombat;
    private bool usingScreenText;

    public class Configs : TweakConfig
    {
        [TweakConfigOption("Always Visible")]
        public bool AlwaysVisible;

        [TweakConfigOption("Hide after combat end (Seconds)", 2, IntMin = 0, IntMax = 60, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
        public int HideDelaySeconds = 20;

        [TweakConfigOption("Font Size", 1, IntMin = 6, IntMax = 255, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
        public int FontSize = 12;

        [TweakConfigOption("X Position Offset", 2, IntMin = -5000, IntMax = 5000, EnforcedLimit = false, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
        public int OffsetX;

        [TweakConfigOption("Y Position Offset", 2, IntMin = -5000, IntMax = 5000, EnforcedLimit = false, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
        public int OffsetY;

        [TweakConfigOption("Text Color", "Color", 3)]
        public Vector4 Color = new Vector4(1, 1, 1, 1);

        [TweakConfigOption("Text Outline Color", "Color", 4)]
        public Vector4 EdgeColor = new Vector4(0xF0, 0x8E, 0x37, 0xFF) / 0xFF;

        [TweakConfigOption("Alternative UI Attachment", 1)]
        public bool UseScreenText;
    }

    [TweakConfig]
    public Configs Config { get; private set; }

    protected override void Disable()
    {
        Service.Condition.ConditionChange -= OnConditionChange;
        Update(true); // Hide the timer
    }

    protected override void Enable()
    {
        Service.Condition.ConditionChange += OnConditionChange;
    }

    private void OnConditionChange(Dalamud.Game.ClientState.Conditions.ConditionFlag flag, bool value)
    {
        if (flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat)
        {
            if (value && !inCombat)
            {
                // Combat started - record the start time
                battleStartTime = DateTime.UtcNow;
                inCombat = true;
            }
            else if (!value && inCombat)
            {
                // Combat ended - record the end time
                lastCombatEndTime = DateTime.UtcNow;
                inCombat = false;
            }
        }
    }

    private bool ShouldShowAfterCombat()
    {
        return !Config.AlwaysVisible &&
               Config.HideDelaySeconds > 0 &&
               lastCombatEndTime != default &&
               (DateTime.UtcNow - lastCombatEndTime).TotalSeconds <= Config.HideDelaySeconds;
    }

    private string FormatTime(TimeSpan duration)
    {
        var totalSeconds = (int)duration.TotalSeconds;
        var maxSeconds = 99 * 3600; // Maximum support for 99 hours

        // Clamp to maximum duration to prevent overflow
        if (totalSeconds > maxSeconds)
            totalSeconds = maxSeconds;

        // Calculate hours, minutes, and seconds
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        // Return appropriate format based on duration
        if (hours > 0)
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}"; // HH:MM:SS
        else
            return $"{minutes:D2}:{seconds:D2}"; // MM:SS
    }

    private unsafe AtkTextNode* FindExistingTextNode(AtkUnitBase* paramWidget, bool reset)
    {
        for (var i = 0; i < paramWidget->UldManager.NodeListCount; i++)
        {
            if (paramWidget->UldManager.NodeList[i] == null) continue;
            if (paramWidget->UldManager.NodeList[i]->NodeId == CustomNodes.CombatTimer)
            {
                var textNode = (AtkTextNode*)paramWidget->UldManager.NodeList[i];
                if (reset)
                {
                    paramWidget->UldManager.NodeList[i]->ToggleVisibility(false);
                    continue;
                }
                return textNode;
            }
        }
        return null;
    }

    private unsafe AtkTextNode* CreateNewTextNode(AtkUnitBase* paramWidget)
    {
        var newTextNode = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
        if (newTextNode == null) return null;

        var lastNode = paramWidget->RootNode;
        if (lastNode == null) return null;

        // Initialize the text node
        IMemorySpace.Memset(newTextNode, 0, (ulong)sizeof(AtkTextNode));
        newTextNode->Ctor();

        // Configure the node properties
        newTextNode->AtkResNode.Type = NodeType.Text;
        newTextNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
        newTextNode->AtkResNode.DrawFlags = 0;
        newTextNode->AtkResNode.SetPositionShort(1, 1);
        newTextNode->AtkResNode.SetWidth(200);
        newTextNode->AtkResNode.SetHeight(14);
        newTextNode->AtkResNode.NodeId = CustomNodes.CombatTimer;

        // Configure text properties
        newTextNode->LineSpacing = 24;
        newTextNode->AlignmentFontType = 0x14;
        newTextNode->FontSize = 12;
        newTextNode->TextFlags = TextFlags.Edge;

        // Set default colors
        newTextNode->AtkResNode.Color.A = 0xFF;
        newTextNode->AtkResNode.Color.R = 0xFF;
        newTextNode->AtkResNode.Color.G = 0xFF;
        newTextNode->AtkResNode.Color.B = 0xFF;

        newTextNode->TextColor.A = 0xFF;
        newTextNode->TextColor.R = 0xFF;
        newTextNode->TextColor.G = 0xFF;
        newTextNode->TextColor.B = 0xFF;

        newTextNode->EdgeColor.A = 0xFF;
        newTextNode->EdgeColor.R = 0xF0;
        newTextNode->EdgeColor.G = 0x8E;
        newTextNode->EdgeColor.B = 0x37;

        // Attach the node to the widget
        AttachTextNodeToWidget(newTextNode, paramWidget, lastNode);

        // Update the widget's draw list
        paramWidget->UldManager.UpdateDrawNodeList();

        return newTextNode;
    }

    private unsafe void AttachTextNodeToWidget(AtkTextNode* newTextNode, AtkUnitBase* paramWidget, AtkResNode* lastNode)
    {
        if (lastNode->ChildNode != null)
        {
            // Find the first child node
            lastNode = lastNode->ChildNode;
            while (lastNode->PrevSiblingNode != null)
            {
                lastNode = lastNode->PrevSiblingNode;
            }

            // Insert before the first child
            newTextNode->AtkResNode.NextSiblingNode = lastNode;
            newTextNode->AtkResNode.ParentNode = paramWidget->RootNode;
            lastNode->PrevSiblingNode = (AtkResNode*)newTextNode;
        }
        else
        {
            // Add as the first child
            lastNode->ChildNode = (AtkResNode*)newTextNode;
            newTextNode->AtkResNode.ParentNode = lastNode;
        }
    }

    private TimeSpan CalculateCurrentDuration()
    {
        if (inCombat)
        {
            // Show live combat duration
            return DateTime.UtcNow - battleStartTime;
        }
        else
        {
            // Show final combat duration (fallback to zero if uninitialized)
            return lastCombatEndTime != default
                ? lastCombatEndTime - battleStartTime
                : TimeSpan.Zero;
        }
    }

    private unsafe void ShowTimer(AtkTextNode* textNode, TimeSpan currentDuration)
    {
        // Make the timer visible
        textNode->AtkResNode.ToggleVisibility(true);

        // Set position
        UiHelper.SetPosition(textNode, -45 + Config.OffsetX, 15 + Config.OffsetY);

        // Configure text properties
        textNode->AlignmentFontType = 0x14;
        textNode->TextFlags |= TextFlags.MultiLine;

        // Apply configured colors
        textNode->EdgeColor.R = (byte)(Config.EdgeColor.X * 0xFF);
        textNode->EdgeColor.G = (byte)(Config.EdgeColor.Y * 0xFF);
        textNode->EdgeColor.B = (byte)(Config.EdgeColor.Z * 0xFF);
        textNode->EdgeColor.A = (byte)(Config.EdgeColor.W * 0xFF);

        textNode->TextColor.R = (byte)(Config.Color.X * 0xFF);
        textNode->TextColor.G = (byte)(Config.Color.Y * 0xFF);
        textNode->TextColor.B = (byte)(Config.Color.Z * 0xFF);
        textNode->TextColor.A = (byte)(Config.Color.W * 0xFF);

        // Apply font settings
        textNode->FontSize = (byte)(Config.FontSize);
        textNode->LineSpacing = (byte)(Config.FontSize);
        textNode->CharSpacing = 1;

        // Set the formatted time text
        var formattedTime = FormatTime(currentDuration);
        textNode->SetText(formattedTime);
    }

    [FrameworkUpdate]
    private void FrameworkUpdate()
    {
        try
        {
            Update();
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    }

    private void Update(bool reset = false)
    {
        // Determine which UI addon to attach the timer to
        var addon = usingScreenText ? "_ScreenText" : "_ParameterWidget";

        // Check if UI attachment mode has changed and reset if necessary
        if (usingScreenText != Config.UseScreenText)
        {
            reset = true;
            usingScreenText = Config.UseScreenText;
        }

        // Hide timer during cutscenes when using screen text
        if (usingScreenText && Service.Condition.Cutscene())
            reset = true;

        // Get the target UI widget
        var paramWidget = Common.GetUnitBase(addon);
        if (paramWidget == null) return;

        // Find existing timer text node or prepare to create a new one
        AtkTextNode* textNode = FindExistingTextNode(paramWidget, reset);
        if (textNode == null && reset) return;

        // Create a new text node if one doesn't exist
        if (textNode == null)
        {
            textNode = CreateNewTextNode(paramWidget);
            if (textNode == null) return;
        }

        // Hide the timer if reset is requested
        if (reset)
        {
            textNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        // Calculate the current duration to display
        TimeSpan currentDuration = CalculateCurrentDuration();

        // Determine if the timer should be visible
        var showTimer = Config.AlwaysVisible || inCombat || ShouldShowAfterCombat();

        if (showTimer)
        {
            // Show and configure the timer display
            ShowTimer(textNode, currentDuration);
        }
        else
        {
            // Hide the timer
            textNode->AtkResNode.ToggleVisibility(false);
        }
    }
}
