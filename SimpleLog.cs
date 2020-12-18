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

        private static string[] SplitMessage(object message) {
            return $"{message}".Split('\n');
        }

        public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach(var m in SplitMessage(message))
#if DEBUG
                PluginLog.LogVerbose($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {m}");
#else
                PluginLog.LogVerbose($"{m}");
#endif
        }

        public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message))
#if DEBUG
                PluginLog.LogDebug($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {m}");
#else
                PluginLog.LogDebug($"{m}");
#endif
        }

        public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message))
#if DEBUG
                PluginLog.LogInformation($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {message}");
#else
                PluginLog.LogInformation($"{m}");
#endif
        }

        public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message))
#if DEBUG
                PluginLog.LogFatal($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {m}");
#else
                PluginLog.LogFatal($"{m}");
#endif
        }

        public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message))
#if DEBUG
                PluginLog.Log($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {m}");
#else
                PluginLog.Log($"{m}");
#endif
        }

        public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message))
#if DEBUG
                PluginLog.Error($"[{callerPath.Substring(subStrIndex)}::{callerName}:{lineNumber}] {m}");
#else
                PluginLog.Error($"{m}");
#endif
        }
    }
}
