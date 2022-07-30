using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Logging;

namespace SimpleTweaksPlugin; 

public static class SimpleLog {

#if DEBUG
    private static int _subStrIndex;
    public static void SetupBuildPath([CallerFilePath] string callerPath = "") {
        var p = Path.GetDirectoryName(callerPath);
        if (p != null) _subStrIndex = p.Length + 1;
    }

    public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogVerbose($"[{callerPath.Substring(_subStrIndex)}::{callerName}:{lineNumber}] {m}");
    }

    public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogDebug($"[{callerPath.Substring(_subStrIndex)}::{callerName}:{lineNumber}] {m}");
    }

    public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogInformation($"[{callerPath.Substring(_subStrIndex)}::{callerName}:{lineNumber}] {message}");
    }

    public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.LogFatal($"[{callerPath.Substring(_subStrIndex)}::{callerName}:{lineNumber}] {m}");
    }

    public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Log($"[{callerPath.Substring(_subStrIndex)}::{callerName}:{lineNumber}] {m}");
    }

    public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
        foreach (var m in SplitMessage(message)) PluginLog.Error($"[{callerPath.Substring(_subStrIndex)}::{callerName}:{lineNumber}] {m}");
    }
#else
        public static void Verbose(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach(var m in SplitMessage(message)) PluginLog.LogVerbose($"{m}");
        }

        public static void Debug(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.LogDebug($"{m}");
        }

        public static void Information(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.LogInformation($"{m}");
        }

        public static void Fatal(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.LogFatal($"{m}");
        }

        public static void Log(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Log($"{m}");
        }

        public static void Error(object message, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = -1) {
            foreach (var m in SplitMessage(message)) PluginLog.Error($"{m}");
        }
#endif

    private static IEnumerable<string> SplitMessage(object message) {
        if (message is IList list) {
            return list.Cast<object>().Select((t, i) => $"{i}: {t}");
        }
        return $"{message}".Split('\n');
    }

}