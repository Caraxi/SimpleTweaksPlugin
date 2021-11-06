using System.IO;

namespace SimpleTweaksPlugin.TweakSystem {
    public class CustomTweakProvider : TweakProvider {

        private readonly TweakLoadContext loadContext;

        public string AssemblyPath { get; }

        private FileInfo LoadedFileInfo;

        public CustomTweakProvider(string path) {
            LoadedFileInfo = new FileInfo(path);
            loadContext = new TweakLoadContext();
            AssemblyPath = path;
            Assembly = loadContext.LoadFromFile(path);
            Service.PluginInterface.UiBuilder.Draw += OnDraw;
        }

        private void OnDraw() {
            if (IsDisposed) {
                Service.PluginInterface.UiBuilder.Draw -= OnDraw;
                return;
            }

            if (Service.PluginInterface.UiBuilder.FrameCount % 100 == 0) {
                var f = new FileInfo(AssemblyPath);
                if (LoadedFileInfo.LastWriteTime != f.LastWriteTime) {
                    SimpleLog.Log($"Detected Change in {AssemblyPath}");
                    Dispose();
                    Loc.ClearCache();
                    SimpleTweaksPlugin.Plugin.LoadCustomProvider(AssemblyPath);
                }
            }
        }

        public override void Dispose() {
            SimpleLog.Log($"Unloading Tweak Provider: {AssemblyPath}");
            Service.PluginInterface.UiBuilder.Draw -= OnDraw;
            base.Dispose();
        }
    }
}
