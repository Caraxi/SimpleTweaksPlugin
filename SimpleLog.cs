using System.IO;
using System.Runtime.CompilerServices;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin {
    internal static class SimpleLog {


#if DEBUG
        private static int subStrIndex = 0;
        public static void SetupBuildPath([CallerFilePath] string callerPath = "") {
            var p = Path.GetDirectoryName(callerPath);
            subStrIndex = p.Length + 1;
        }
#endif


        public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
#if DEBUG
            PluginLog.LogVerbose($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
            PluginLog.LogVerbose($"{message}");
#endif
        }

        public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
#if DEBUG
            PluginLog.LogDebug($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
            PluginLog.LogDebug($"{message}");
#endif
        }

        public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
#if DEBUG
            PluginLog.LogInformation($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
            PluginLog.LogInformation($"{message}");
#endif
        }

        public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
#if DEBUG
            PluginLog.LogFatal($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
            PluginLog.LogFatal($"{message}");
#endif
        }

        public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
#if DEBUG
            PluginLog.Log($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
            PluginLog.Log($"{message}");
#endif
        }

        public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
#if DEBUG
            PluginLog.Error($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
            PluginLog.Error($"{message}");
#endif
        }
    }
}
