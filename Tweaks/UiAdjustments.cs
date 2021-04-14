using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public UiAdjustmentsConfig UiAdjustments = new UiAdjustmentsConfig();
    }

    public partial class UiAdjustmentsConfig { }
}

namespace SimpleTweaksPlugin.Tweaks {
    public class UiAdjustments : SubTweakManager<UiAdjustments.SubTweak> {
        public override bool AlwaysEnabled => true;

        public abstract class SubTweak : BaseTweak {
            public override string Key => $"{nameof(UiAdjustments)}@{base.Key}";
        }

        public override string Name => "UI Tweaks";

        public enum Step {
            Parent,
            Child,
            Previous,
            Next,
            PrevFinal
        }

        public static unsafe AtkResNode* GetResNodeByPath(AtkResNode* root, params Step[] steps) {
            
            var current = root;
            foreach (var step in steps) {
                if (current == null) return null;

                current = step switch {
                    Step.Parent => current->ParentNode,
                    Step.Child => (ushort) current->Type >= 1000 ? ((AtkComponentNode*)current)->Component->UldManager.RootNode : current->ChildNode,
                    Step.Next => current->NextSiblingNode,
                    Step.Previous => current->PrevSiblingNode,
                    Step.PrevFinal => FinalPreviousNode(current),
                    _ => null,
                };
            }
            return current;
        }
        private static unsafe AtkResNode* FinalPreviousNode(AtkResNode* node) {
            while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;
            return node;
        }
        public static unsafe AtkResNode** CopyNodeList(AtkResNode** originalList, ushort originalSize, ushort newSize = 0) {
            if (newSize <= originalSize) newSize = (ushort) (originalSize + 1);
            var oldListPtr = new IntPtr(originalList);
            var newListPtr = Common.Alloc((ulong)((newSize + 1) * 8));
            var clone = new IntPtr[originalSize];
            Marshal.Copy(oldListPtr, clone, 0, originalSize);
            Marshal.Copy(clone, 0, newListPtr, originalSize);
            return (AtkResNode**)(newListPtr);
        }
        public static unsafe AtkTextNode* CloneNode(AtkTextNode* original, bool autoInsert = true) {
            var newAllocation = Common.Alloc((ulong) sizeof(AtkTextNode));

            var bytes = new byte[sizeof(AtkTextNode)];
            Marshal.Copy(new IntPtr(original), bytes, 0, bytes.Length);
            Marshal.Copy(bytes, 0, newAllocation, bytes.Length);

            var newNode = (AtkTextNode*) newAllocation;

            newNode->AtkResNode.NextSiblingNode = (AtkResNode*) original;
            original->AtkResNode.PrevSiblingNode = (AtkResNode*) newNode;
            if (newNode->AtkResNode.PrevSiblingNode != null) {
                newNode->AtkResNode.NextSiblingNode = (AtkResNode*) newNode;
            }

            newNode->AtkResNode.ParentNode->ChildCount += 1;
            return newNode;
        }

        public static unsafe AtkResNode* CloneNode(AtkResNode* original) {
            var size = original->Type switch {
                NodeType.Res => sizeof(AtkResNode),
                NodeType.Image => sizeof(AtkImageNode),
                NodeType.Text => sizeof(AtkTextNode),
                NodeType.NineGrid => sizeof(AtkNineGridNode),
                NodeType.Counter => sizeof(AtkCounterNode),
                NodeType.Collision => sizeof(AtkCollisionNode),
                _ => throw new Exception("Unsupported Type")
            };

            var allocation = Common.Alloc((ulong) size);
            var bytes = new byte[size];
            Marshal.Copy(new IntPtr(original), bytes, 0, bytes.Length);
            Marshal.Copy(bytes, 0, allocation, bytes.Length);

            var newNode = (AtkResNode*) allocation;
            newNode->ParentNode = null;
            newNode->ChildNode = null;
            newNode->ChildCount = 0;
            newNode->PrevSiblingNode = null;
            newNode->NextSiblingNode = null;
            return newNode;
        }
    }
}
