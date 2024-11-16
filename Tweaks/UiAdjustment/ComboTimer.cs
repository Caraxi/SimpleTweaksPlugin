using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Action = Lumina.Excel.Sheets.Action;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Combo Timer")]
[TweakDescription("Shows a countdown for combo actions.")]
[TweakAutoConfig]
public unsafe class ComboTimer : UiAdjustments.SubTweak {
    private readonly Dictionary<uint, byte> comboActions = new() {
        [7526] = 80,
    };
    private bool usingScreenText;

    public class Configs : TweakConfig {
        [TweakConfigOption("Always Visible")] public bool AlwaysVisible;

        [TweakConfigOption("Hide 'COMBO' Text")]
        public bool NoComboText;

        [TweakConfigOption("Font Size", 1, IntMin = 6, IntMax = 255, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int FontSize = 12;

        [TweakConfigOption("X Position Offset", 2, IntMin = -5000, IntMax = 5000, EnforcedLimit = false, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
        public int OffsetX;

        [TweakConfigOption("Y Position Offset", 2, IntMin = -5000, IntMax = 5000, EnforcedLimit = false, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
        public int OffsetY;

        [TweakConfigOption("Text Color", "Color", 3)]
        public Vector4 Color = new Vector4(1, 1, 1, 1);

        [TweakConfigOption("Text Outline Color", "Color", 4)]
        public Vector4 EdgeColor = new Vector4(0xF0, 0x8E, 0x37, 0xFF) / 0xFF;

        [TweakConfigOption("Leading Zero")] public bool LeadingZero = true;

        [TweakConfigOption("Decimal Places", 3, IntMin = 0, IntMax = 4, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int DecimalPlaces = 2;

        [TweakConfigOption("Alternative UI Attachment", 1)]
        public bool UseScreenText;
    }

    [TweakConfig] public Configs Config { get; private set; }

    protected override void Disable() {
        Update(true);
    }

    [FrameworkUpdate]
    private void FrameworkUpdate() {
        try {
            Update();
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void Update(bool reset = false) {
        var addon = usingScreenText ? "_ScreenText" : "_ParameterWidget";
        if (usingScreenText != Config.UseScreenText) {
            reset = true;
            usingScreenText = Config.UseScreenText;
        }

        if (usingScreenText && Service.Condition.Cutscene()) reset = true;

        var paramWidget = Common.GetUnitBase(addon);
        if (paramWidget == null) return;

        AtkTextNode* textNode = null;
        for (var i = 0; i < paramWidget->UldManager.NodeListCount; i++) {
            if (paramWidget->UldManager.NodeList[i] == null) continue;
            if (paramWidget->UldManager.NodeList[i]->NodeId == CustomNodes.ComboTimer) {
                textNode = (AtkTextNode*)paramWidget->UldManager.NodeList[i];
                if (reset) {
                    paramWidget->UldManager.NodeList[i]->ToggleVisibility(false);
                    continue;
                }

                break;
            }
        }

        if (textNode == null && reset) return;

        if (textNode == null) {
            var newTextNode = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
            if (newTextNode != null) {
                var lastNode = paramWidget->RootNode;
                if (lastNode == null) return;

                IMemorySpace.Memset(newTextNode, 0, (ulong)sizeof(AtkTextNode));
                newTextNode->Ctor();
                textNode = newTextNode;

                newTextNode->AtkResNode.Type = NodeType.Text;
                newTextNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
                newTextNode->AtkResNode.DrawFlags = 0;
                newTextNode->AtkResNode.SetPositionShort(1, 1);
                newTextNode->AtkResNode.SetWidth(200);
                newTextNode->AtkResNode.SetHeight(14);

                newTextNode->LineSpacing = 24;
                newTextNode->AlignmentFontType = 0x14;
                newTextNode->FontSize = 12;
                newTextNode->TextFlags = (byte)(TextFlags.Edge);
                newTextNode->TextFlags2 = 0;

                newTextNode->AtkResNode.NodeId = CustomNodes.ComboTimer;

                newTextNode->AtkResNode.Color.A = 0xFF;
                newTextNode->AtkResNode.Color.R = 0xFF;
                newTextNode->AtkResNode.Color.G = 0xFF;
                newTextNode->AtkResNode.Color.B = 0xFF;

                if (lastNode->ChildNode != null) {
                    lastNode = lastNode->ChildNode;
                    while (lastNode->PrevSiblingNode != null) {
                        lastNode = lastNode->PrevSiblingNode;
                    }

                    newTextNode->AtkResNode.NextSiblingNode = lastNode;
                    newTextNode->AtkResNode.ParentNode = paramWidget->RootNode;
                    lastNode->PrevSiblingNode = (AtkResNode*)newTextNode;
                } else {
                    lastNode->ChildNode = (AtkResNode*)newTextNode;
                    newTextNode->AtkResNode.ParentNode = lastNode;
                }

                textNode->TextColor.A = 0xFF;
                textNode->TextColor.R = 0xFF;
                textNode->TextColor.G = 0xFF;
                textNode->TextColor.B = 0xFF;

                textNode->EdgeColor.A = 0xFF;
                textNode->EdgeColor.R = 0xF0;
                textNode->EdgeColor.G = 0x8E;
                textNode->EdgeColor.B = 0x37;

                paramWidget->UldManager.UpdateDrawNodeList();
            }
        }

        if (reset) {
            textNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        var combo = &ActionManager.Instance()->Combo;

        if (combo->Action != 0 && !comboActions.ContainsKey(combo->Action)) {
            comboActions.Add(combo->Action, Service.Data.Excel.GetSheet<Action>().OrderBy(a => a.ClassJobLevel).FirstOrNull(a => a.ActionCombo.RowId == combo->Action)?.ClassJobLevel ?? 255);
        }

        var comboAvailable = Service.ClientState?.LocalPlayer != null && combo->Timer > 0 && combo->Action != 0 && comboActions.ContainsKey(combo->Action) && comboActions[combo->Action] <= Service.ClientState.LocalPlayer.Level;

        if (Config.AlwaysVisible || comboAvailable) {
            textNode->AtkResNode.ToggleVisibility(true);
            UiHelper.SetPosition(textNode, -45 + Config.OffsetX, 15 + Config.OffsetY);
            textNode->AlignmentFontType = 0x14;
            textNode->TextFlags |= (byte)TextFlags.MultiLine;

            textNode->EdgeColor.R = (byte)(Config.EdgeColor.X * 0xFF);
            textNode->EdgeColor.G = (byte)(Config.EdgeColor.Y * 0xFF);
            textNode->EdgeColor.B = (byte)(Config.EdgeColor.Z * 0xFF);
            textNode->EdgeColor.A = (byte)(Config.EdgeColor.W * 0xFF);

            textNode->TextColor.R = (byte)(Config.Color.X * 0xFF);
            textNode->TextColor.G = (byte)(Config.Color.Y * 0xFF);
            textNode->TextColor.B = (byte)(Config.Color.Z * 0xFF);
            textNode->TextColor.A = (byte)(Config.Color.W * 0xFF);

            textNode->FontSize = (byte)(Config.FontSize);
            textNode->LineSpacing = (byte)(Config.FontSize);
            textNode->CharSpacing = 1;
            var comboTimer = (comboAvailable ? combo->Timer : 0.0f).ToString($"{(Config.LeadingZero ? "00" : "0")}{(Config.DecimalPlaces > 0 ? "." + new string('0', Config.DecimalPlaces) : "")}");
            textNode->SetText(Config.NoComboText ? $"{comboTimer}" : $"Combo\n{comboTimer}");
        } else {
            textNode->AtkResNode.ToggleVisibility(false);
        }
    }
}
