using System.Numerics;
using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin {
    public static unsafe partial class UiHelper {
        public static void Hide(AtkTextNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkNineGridNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkImageNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkComponentNode* node) => Hide((AtkResNode*)node);

        public static void Show(AtkTextNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkNineGridNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkImageNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkComponentNode* node) => Show((AtkResNode*)node);

        public static void SetSize(AtkComponentNode* node, ushort? w, ushort? h) => SetSize((AtkResNode*) node, w, h);
        public static void SetSize(AtkTextNode* node, ushort? w, ushort? h) => SetSize((AtkResNode*) node, w, h);

        public static void SetPosition(AtkComponentNode* node, float? x, float? y) => SetPosition((AtkResNode*) node, x, y);
        public static void SetPosition(AtkTextNode* node, float? x, float? y) => SetPosition((AtkResNode*) node, x, y);
    }
}
