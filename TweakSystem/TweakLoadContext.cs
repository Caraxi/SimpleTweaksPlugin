using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin;
using FFXIVClientStructs.Interop;

namespace SimpleTweaksPlugin.TweakSystem; 

public class TweakLoadContext : AssemblyLoadContext {

    private DirectoryInfo directory;
    private string name;
    private record MonitoredPath(string path, DateTime modified);
    private List<MonitoredPath> monitoredPaths = new();
    
    public TweakLoadContext(string name, DirectoryInfo directoryInfo) : base(true) {
        this.name = name;
        directory = directoryInfo;
    }

    private static Dictionary<string, Assembly> handledAssemblies;

    static TweakLoadContext() {
        handledAssemblies = new Dictionary<string, Assembly>() {
            ["SimpleTweaksPlugin"] = typeof(SimpleTweaksPlugin).Assembly,
            ["FFXIVClientStructs"] = typeof(FFXIVClientStructs.ThisAssembly).Assembly,
            ["Dalamud"] = typeof(IDalamudPluginInterface).Assembly,
        };
    }
    
    protected override Assembly? Load(AssemblyName assemblyName) {
        SimpleLog.Log($"[{name}] Attempting to load {assemblyName.FullName}");
        
        if (assemblyName.Name == "FFXIVClientStructs") {
            var csFilePath = Path.Join(directory.FullName, "FFXIVClientStructs.dll");
            var csFile = new FileInfo(csFilePath);
            if (csFile.Exists) {
                SimpleLog.Log($"[{name}] Attempting to load custom FFXIVClientStructs from {csFile.FullName}");
                monitoredPaths.Add(new MonitoredPath(csFile.FullName, csFile.LastWriteTime));
                return LoadFromFile(csFile.FullName);
            }
        }
        
        if (assemblyName.Name != null && handledAssemblies.ContainsKey(assemblyName.Name)) {
            SimpleLog.Log($"[{name}] Forwarded reference to {assemblyName.Name}");
            return handledAssemblies[assemblyName.Name];
        }
        
        var filePath = Path.Join(directory.FullName, $"{assemblyName.Name}.dll");
        var file = new FileInfo(filePath);
        if (file.Exists) {
            try {
                SimpleLog.Log($"[{name}] Attempting to load {assemblyName.Name} from {file.FullName}");
                monitoredPaths.Add(new MonitoredPath(file.FullName, file.LastWriteTime));
                return LoadFromFile(file.FullName);
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

    public bool DetectChanges() {
        foreach (var m in monitoredPaths) {
            var f = new FileInfo(m.path);
            if (f.Exists && f.LastWriteTime != m.modified) {
                SimpleLog.Log($"Loaded Assembly Changed: {f.FullName}");
                return true;
            }
        }

        return false;
    }
}