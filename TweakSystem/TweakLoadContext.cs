using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin;
using FFXIVClientStructs;
using XivCommon;

namespace SimpleTweaksPlugin.TweakSystem; 

public class TweakLoadContext : AssemblyLoadContext {

    private DirectoryInfo directory;
    private string name;
    
    
    public TweakLoadContext(string name, DirectoryInfo directoryInfo) {
        directory = directoryInfo;
    }


    private static Dictionary<string, Assembly> handledAssemblies;

    static TweakLoadContext() {
        handledAssemblies = new Dictionary<string, Assembly>() {
            ["SimpleTweaksPlugin"] = Assembly.GetExecutingAssembly(),
            ["FFXIVClientStructs"] = typeof(Resolver).Assembly,
            ["Dalamud"] = typeof(DalamudPluginInterface).Assembly,
            ["XivCommon"] = typeof(XivCommonBase).Assembly
        };
    }
    
    
    
    protected override Assembly? Load(AssemblyName assemblyName) {
        SimpleLog.Log($"[{name}] Attempting to load {assemblyName.FullName}");

        if (assemblyName.Name != null && handledAssemblies.ContainsKey(assemblyName.Name)) {
            SimpleLog.Log($"[{name}] Forwarded reference to {assemblyName.Name}");
            return handledAssemblies[assemblyName.Name];
        }
        
        var file = Path.Join(directory.FullName, $"{assemblyName.Name}.dll");
        if (File.Exists(file)) {
            try {
                SimpleLog.Log($"[{name}] Attempting to load {assemblyName.Name} from {file}");
                return LoadFromFile(file);
            } catch {
                //
            }
        }
        
        return base.Load(assemblyName);
    }

    public Assembly LoadFromFile(string filePath) {
        using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var pdbPath = Path.ChangeExtension(filePath, ".pdb");
        if (!File.Exists(pdbPath)) return LoadFromStream(file);
        using var pdbFile = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return LoadFromStream(file, pdbFile);
    }
}