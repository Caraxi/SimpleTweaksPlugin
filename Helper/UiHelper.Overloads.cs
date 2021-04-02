using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Helper {
    public static unsafe partial class UiHelper {
        public static void Hide(AtkTextNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkNineGridNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkImageNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkComponentNode* node) => Hide((AtkResNode*)node);
        public static void Hide(AtkCollisionNode* node) => Hide((AtkResNode*)node);

        public static void Show(AtkTextNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkNineGridNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkImageNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkComponentNode* node) => Show((AtkResNode*)node);
        public static void Show(AtkCollisionNode* node) => Show((AtkResNode*)node);

        public static void SetSize(AtkComponentNode* node, int? w, int? h) => SetSize((AtkResNode*) node, w, h);
        public static void SetSize(AtkTextNode* node, int? w, int? h) => SetSize((AtkResNode*) node, w, h);
        public static void SetSize(AtkCollisionNode* node, int? w, int? h) => SetSize((AtkResNode*)node, w, h);

        public static void SetPosition(AtkComponentNode* node, float? x, float? y) => SetPosition((AtkResNode*) node, x, y);
        public static void SetPosition(AtkTextNode* node, float? x, float? y) => SetPosition((AtkResNode*) node, x, y);
        public static void SetPosition(AtkCollisionNode* node, float? x, float? y) => SetPosition((AtkResNode*)node, x, y);

        public static AtkTextNode* CloneNode(AtkTextNode* node) => (AtkTextNode*) CloneNode((AtkResNode*) node);
    }
}
