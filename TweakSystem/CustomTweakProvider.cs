using System;
using System.IO;

namespace SimpleTweaksPlugin.TweakSystem; 

public class CustomTweakProviderConfig {
    public string Assembly;
    public bool Enabled;
}

public class CustomTweakProvider : TweakProvider {

    private readonly TweakLoadContext loadContext;
    private readonly CustomTweakProviderConfig config;

    public string AssemblyPath { get; }
    
    private DateTime lastWriteTime;

    public CustomTweakProvider(CustomTweakProviderConfig config) {
        this.config = config;
        var loadedFileInfo = new FileInfo(this.config.Assembly);
        lastWriteTime = loadedFileInfo.LastWriteTime;
        loadContext = new TweakLoadContext(loadedFileInfo.Name, loadedFileInfo.Directory ?? throw new Exception("Invalid file path"));
        AssemblyPath = config.Assembly;
        Assembly = loadContext.LoadFromFile(this.config.Assembly);
        Service.PluginInterface.UiBuilder.Draw += OnDraw;
    }
    
    private void OnDraw() {
        if (IsDisposed) {
            Service.PluginInterface.UiBuilder.Draw -= OnDraw;
            return;
        }

        if (Service.PluginInterface.UiBuilder.FrameCount % 100 == 0) {
            
            var f = new FileInfo(AssemblyPath);
            if (f.Exists) {
                if (lastWriteTime != f.LastWriteTime || loadContext.DetectChanges()) {
                    SimpleLog.Log($"Detected Change in {AssemblyPath}");
                    Dispose();
                    Loc.ClearCache();
                    SimpleTweaksPlugin.Plugin.LoadCustomProvider(config);
                }
            }
        }
    }

    public override void Dispose() {
        SimpleLog.Log($"Unloading Tweak Provider: {AssemblyPath}");
        Service.PluginInterface.UiBuilder.Draw -= OnDraw;
        base.Dispose();
    }
}
