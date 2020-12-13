using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public UiAdjustmentsConfig UiAdjustments = new UiAdjustmentsConfig();
    }

    public partial class UiAdjustmentsConfig { }
}

namespace SimpleTweaksPlugin.Tweaks {
    public class UiAdjustments : SubTweakManager<UiAdjustments.SubTweak> {

        public abstract class SubTweak : BaseTweak { }

        public override string Name => "UI Adjustments";

        public enum Step {
            Parent,
            Child,
            Previous,
            Next,
        }

        public static unsafe AtkResNode* GetResNodeByPath(AtkResNode* root, params Step[] steps) {
            
            var current = root;
            foreach (var step in steps) {
                if (current == null) return null;
                current = step switch {
                    Step.Parent => current->ParentNode,
                    Step.Child => current->ChildNode,
                    Step.Next => current->NextSiblingNode,
                    Step.Previous => current->PrevSiblingNode,
                    _ => null,
                };
            }
            return current;
        }
    }

}
