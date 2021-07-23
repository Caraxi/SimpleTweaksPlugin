using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ComboTimer : UiAdjustments.SubTweak {
        public override string Name => "Combo Timer";
        public override string Description => "Shows a countdown for combo actions.";

        private readonly Dictionary<uint, byte> comboActions = new();
        
        public class Configs : TweakConfig {
            [TweakConfigOption("Always Visible")]
            public bool AlwaysVisible = false;

            [TweakConfigOption("Hide 'COMBO' Text")]
            public bool NoComboText = false;

            [TweakConfigOption("Font Size", 1, IntMin = 6, IntMax = 255, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
            public int FontSize = 12;
            
            [TweakConfigOption("X Position Offset", 2, IntMin = -2000, IntMax = 2000, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
            public int OffsetX;
            
            [TweakConfigOption("Y Position Offset", 2, IntMin = -2000, IntMax = 2000, IntType = TweakConfigOptionAttribute.IntEditType.Drag, EditorSize = 150)]
            public int OffsetY;
            
            [TweakConfigOption("Text Color", "Color", 3)]
            public Vector4 Color = new Vector4(1, 1, 1, 1);
            
            [TweakConfigOption("Text Outline Color", "Color", 4)]
            public Vector4 EdgeColor = new Vector4(0xF0, 0x8E, 0x37, 0xFF) / 0xFF;
            
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
            if (combo == null) combo = (Combo*) Common.Scanner.GetStaticAddressFromSig("48 89 2D ?? ?? ?? ?? 85 C0");
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }
        
        public override void Disable() {
            SaveConfig(Config);
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
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
            var paramWidget = Common.GetUnitBase("_ParameterWidget");
            if (paramWidget == null) return;
            
            AtkTextNode* textNode = null;
            for (var i = 0; i < paramWidget->UldManager.NodeListCount; i++) {
                var node = paramWidget->UldManager.NodeList[i];
                if (node->Type == NodeType.Text && node->NodeID == CustomNodes.ComboTimer) {
                    textNode = (AtkTextNode*) node;
                    break;
                }
            }

            if (textNode == null && reset) return;

            if (textNode == null) {
                textNode = UiHelper.CloneNode((AtkTextNode*) paramWidget->UldManager.NodeList[3]);
                textNode->AtkResNode.NodeID = CustomNodes.ComboTimer;
                var newStrPtr = Common.Alloc(512);
                textNode->NodeText.StringPtr = (byte*) newStrPtr;
                textNode->NodeText.BufSize = 512;
                UiHelper.SetText(textNode, "00.00");
                UiHelper.ExpandNodeList(paramWidget, 1);
                paramWidget->UldManager.NodeList[paramWidget->UldManager.NodeListCount++] = (AtkResNode*) textNode;
                
                textNode->AtkResNode.ParentNode = paramWidget->UldManager.NodeList[3]->ParentNode;
                textNode->AtkResNode.ChildNode = null;
                textNode->AtkResNode.PrevSiblingNode = null;
                textNode->AtkResNode.NextSiblingNode = paramWidget->UldManager.NodeList[3];
                if(paramWidget->UldManager.NodeList[3]->PrevSiblingNode != null) {
                    textNode->AtkResNode.PrevSiblingNode = paramWidget->UldManager.NodeList[3]->PrevSiblingNode;
                }
                paramWidget->UldManager.NodeList[3]->PrevSiblingNode = (AtkResNode*) textNode;
            }

            if (reset) {
                UiHelper.Hide(textNode);
                return;
            }

            if (combo->Action != 0 && !comboActions.ContainsKey(combo->Action)) {
                comboActions.Add(combo->Action, PluginInterface.Data.Excel.GetSheet<Action>().OrderBy(a => a.ClassJobLevel).FirstOrDefault(a => a.ActionCombo.Row == combo->Action)?.ClassJobLevel ?? 255);
            }
            
            var comboAvailable = combo->Timer > 0 && combo->Action != 0 && comboActions.ContainsKey(combo->Action) && comboActions[combo->Action] <= PluginInterface.ClientState.LocalPlayer.Level;
            
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
                if (comboAvailable) {
                    UiHelper.SetText(textNode, Config.NoComboText ? $"{combo->Timer:00.00}" : $"Combo\n{combo->Timer:00.00}");
                } else {
                    UiHelper.SetText(textNode, Config.NoComboText ? $"00.00" : $"Combo\n00.00");
                }
                
            } else { 
                UiHelper.Hide(textNode);
            }
        }
    }
}
