using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ComboTimer : UiAdjustments.SubTweak {
    public override string Name => "Combo Timer";
    public override string Description => "Shows a countdown for combo actions.";

    private readonly Dictionary<uint, byte> comboActions = new();
    private bool usingScreenText = false;
        
    public class Configs : TweakConfig {
        [TweakConfigOption("Always Visible")]
        public bool AlwaysVisible = false;

        [TweakConfigOption("Hide 'COMBO' Text")]
        public bool NoComboText = false;

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
            
        [TweakConfigOption("Leading Zero")]
        public bool LeadingZero = true;

        [TweakConfigOption("Decimal Places", 3, IntMin = 0, IntMax = 4, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int DecimalPlaces = 2;

        [TweakConfigOption("Alternative UI Attachment", 1)]
        public bool UseScreenText = false;
    }
        
    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;
    private Combo* combo;

    [StructLayout(LayoutKind.Explicit, Size = 0x8)]
    public struct Combo {
        [FieldOffset(0x00)] public float Timer;
        [FieldOffset(0x04)] public uint Action;
    }
        
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        if (combo == null) combo = (Combo*) Common.Scanner.GetStaticAddressFromSig("F3 0F 11 05 ?? ?? ?? ?? F3 0F 10 45");
        Service.Framework.Update += FrameworkUpdate;
        base.Enable();
    }
        
    public override void Setup() {
        comboActions.Add(7526, 80); // Verholy to Scorch Combo
        base.Setup();
    }

    public override void Disable() {
        SaveConfig(Config);
        Service.Framework.Update -= FrameworkUpdate;
        Update(true);
        base.Disable();
    }

    private void FrameworkUpdate(Framework framework) {
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
            if (paramWidget->UldManager.NodeList[i]->NodeID == CustomNodes.ComboTimer) {
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
                newTextNode->AtkResNode.Flags = (short)(NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
                newTextNode->AtkResNode.DrawFlags = 0;
                newTextNode->AtkResNode.SetPositionShort(1, 1);
                newTextNode->AtkResNode.SetWidth(200);
                newTextNode->AtkResNode.SetHeight(14);

                newTextNode->LineSpacing = 24;
                newTextNode->AlignmentFontType = 0x14;
                newTextNode->FontSize = 12;
                newTextNode->TextFlags = (byte)(TextFlags.Edge);
                newTextNode->TextFlags2 = 0;

                newTextNode->AtkResNode.NodeID = CustomNodes.ComboTimer;

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
                    lastNode->PrevSiblingNode = (AtkResNode*) newTextNode;
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
            UiHelper.Hide(textNode);
            return;
        }

        if (combo->Action != 0 && !comboActions.ContainsKey(combo->Action)) {
            comboActions.Add(combo->Action, Service.Data.Excel.GetSheet<Action>().OrderBy(a => a.ClassJobLevel).FirstOrDefault(a => a.ActionCombo.Row == combo->Action)?.ClassJobLevel ?? 255);
        }
            
        var comboAvailable = Service.ClientState?.LocalPlayer != null && combo->Timer > 0 && combo->Action != 0 && comboActions.ContainsKey(combo->Action) && comboActions[combo->Action] <= Service.ClientState.LocalPlayer.Level;
            
        if (Config.AlwaysVisible || comboAvailable) {
            UiHelper.Show(textNode);
            UiHelper.SetPosition(textNode, -45 + Config.OffsetX, 15 + Config.OffsetY);
            textNode->AlignmentFontType = 0x14;
            textNode->TextFlags |= (byte) TextFlags.MultiLine;
                
            textNode->EdgeColor.R = (byte) (this.Config.EdgeColor.X * 0xFF);
            textNode->EdgeColor.G = (byte) (this.Config.EdgeColor.Y * 0xFF);
            textNode->EdgeColor.B = (byte) (this.Config.EdgeColor.Z * 0xFF);
            textNode->EdgeColor.A = (byte) (this.Config.EdgeColor.W * 0xFF);
                
            textNode->TextColor.R = (byte) (this.Config.Color.X * 0xFF);
            textNode->TextColor.G = (byte) (this.Config.Color.Y * 0xFF);
            textNode->TextColor.B = (byte) (this.Config.Color.Z * 0xFF);
            textNode->TextColor.A = (byte) (this.Config.Color.W * 0xFF);

            textNode->FontSize = (byte) (this.Config.FontSize);
            textNode->LineSpacing = (byte) (this.Config.FontSize);
            textNode->CharSpacing = 1;
            var comboTimer = (comboAvailable ? combo->Timer : 0.0f).ToString($"{(Config.LeadingZero ? "00" : "0")}{(Config.DecimalPlaces>0 ? "." + new string('0', Config.DecimalPlaces) : "")}");
            textNode->SetText(Config.NoComboText ? $"{comboTimer}" : $"Combo\n{comboTimer}");
        } else { 
            UiHelper.Hide(textNode);
        }
    }
}