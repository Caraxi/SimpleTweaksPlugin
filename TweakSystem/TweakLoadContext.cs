using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace SimpleTweaksPlugin.TweakSystem {
    public class TweakLoadContext : AssemblyLoadContext {
        protected override Assembly? Load(AssemblyName assemblyName) {
            var currentAssembly = Assembly.GetExecutingAssembly();
            var currentAssemblyName = currentAssembly.GetName();
            return assemblyName.Name == currentAssemblyName.Name ? currentAssembly : base.Load(assemblyName);
        }

        public Assembly LoadFromFile(string filePath) {
            using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var pdbPath = Path.ChangeExtension(filePath, ".pdb");
            if (!File.Exists(pdbPath)) return LoadFromStream(file);
            using var pdbFile = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return LoadFromStream(file, pdbFile);
        }
    }
}
