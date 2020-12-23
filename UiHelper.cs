using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;

namespace SimpleTweaksPlugin {
    public static unsafe partial class UiHelper {

        public static void Hide(AtkResNode* node) {
            node->Flags &= ~0x10;
            node->Flags_2 |= 0x1;
        }
        public static void Show(AtkResNode* node) {
            node->Flags |= 0x10;
            node->Flags_2 |= 0x1;
        }

        public static void SetSize(AtkResNode* node, ushort? width, ushort? height) {
            if (width != null) node->Width = width.Value;
            if (height != null) node->Height = height.Value;
            node->Flags_2 |= 0x1;
        }

        public static void SetPosition(AtkResNode* node, float? x, float? y) {
            if (x != null) node->X = x.Value;
            if (y != null) node->Y = y.Value;
            node->Flags_2 |= 0x1;
        }

        public static void SetWindowSize(AtkComponentNode* windowNode, ushort? width, ushort? height) {
            if (((ULDComponentInfo*) windowNode->Component->ULDData.Objects)->ComponentType != (byte) ComponentType.Window) return;

            width ??= windowNode->AtkResNode.Width;
            height ??= windowNode->AtkResNode.Height;

            if (width < 64) width = 64;

            SetSize(windowNode, width, height);  // Window
            var n = windowNode->Component->ULDData.RootNode;
            SetSize(n, width, height);  // Collision
            n = n->PrevSiblingNode;
            SetSize(n, (ushort)(width - 14), null); // Header Collision
            n = n->PrevSiblingNode;
            SetSize(n, width, height); // Background
            n = n->PrevSiblingNode;
            SetSize(n, width, height); // Focused Border
            n = n->PrevSiblingNode;
            SetSize(n, (ushort) (width - 5), null); // Header Node
            n = n->ChildNode;
            SetSize(n, (ushort) (width - 20), null); // Header Seperator
            n = n->PrevSiblingNode;
            SetPosition(n, width - 33, 6); // Close Button
            n = n->PrevSiblingNode;
            SetPosition(n, width - 47, 8); // Gear Button
            n = n->PrevSiblingNode;
            SetPosition(n, width - 61, 8); // Help Button

            windowNode->AtkResNode.Flags_2 |= 0x1;
        }

    }
}
