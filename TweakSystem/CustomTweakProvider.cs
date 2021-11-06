
namespace SimpleTweaksPlugin.TweakSystem {
    public class CustomTweakProvider : TweakProvider {

        private readonly TweakLoadContext loadContext;

        public string AssemblyPath { get; }


        public CustomTweakProvider(string path) {
            SimpleLog.Log("");
            loadContext = new TweakLoadContext();
            AssemblyPath = path;
            Assembly = loadContext.LoadFromFile(path);
        }

        public bool



        public override void Dispose() {
            SimpleLog.Log($"Unloading Tweak Provider: {AssemblyPath}");
            base.Dispose();
        }
    }
}
